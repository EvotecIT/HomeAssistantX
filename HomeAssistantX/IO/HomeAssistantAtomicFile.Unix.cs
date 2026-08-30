using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    // Preserve ordinary permissions and the sticky bit, but never restore
    // setuid/setgid onto an inode containing newly exported bytes.
    private const int SafeReplacementModeBits = 0x03FF;
    private const int UnixFileTypeBits = 0xF000;
    private const int UnixSymbolicLink = 0xA000;
    private const string LinuxAccessAclAttribute = "system.posix_acl_access";
    private const int UnixNoEntry = 2;
    private const int LinuxNoData = 61;
    private const int LinuxOperationNotSupported = 95;
    private const int MacExtendedAcl = 0x00000100;
    private const uint OwnerReadWriteMode = 0x180;
    private const int LinuxWriteOnly = 0x0001;
    private const int LinuxCreate = 0x0040;
    private const int LinuxExclusive = 0x0080;
    private const int LinuxCloseOnExec = 0x80000;
    private const int MacWriteOnly = 0x0001;
    private const int MacCreate = 0x0200;
    private const int MacExclusive = 0x0800;
    private const int MacCloseOnExec = 0x1000000;
    private const int UnixCurrentWorkingDirectory = -100;
    private const uint LinuxRenameExchange = 0x2;
    private const uint MacRenameSwap = 0x2;

    private static void CommitUnixOverwrite(
        string temporaryPath,
        string destinationPath,
        CancellationToken cancellationToken,
        Action? beforeMetadataRecheck,
        Action? afterExchange)
    {
        cancellationToken.ThrowIfCancellationRequested();
        beforeMetadataRecheck?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();

        var exchangeResult = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? RenameMac(temporaryPath, destinationPath, MacRenameSwap)
            : RenameLinux(
                UnixCurrentWorkingDirectory,
                temporaryPath,
                UnixCurrentWorkingDirectory,
                destinationPath,
                LinuxRenameExchange);
        if (exchangeResult == 0)
        {
            // The displaced destination now has temporaryPath. Reading it after the
            // atomic exchange removes the compare-to-rename race; the replacement
            // remains restrictive until those exact permissions are applied.
            try
            {
                // Once the paths have been exchanged this is a non-cancellable
                // commit section: either finish or restore the displaced file.
                afterExchange?.Invoke();
                var displaced = ReadUnixFileMetadata(temporaryPath);
                ApplyUnixDestinationMetadata(
                    temporaryPath,
                    destinationPath,
                    displaced,
                    useManagedApis: true);
                File.Delete(temporaryPath);
            }
            catch (Exception commitException)
            {
                var rollbackResult = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? RenameMac(destinationPath, temporaryPath, MacRenameSwap)
                    : RenameLinux(
                        UnixCurrentWorkingDirectory,
                        destinationPath,
                        UnixCurrentWorkingDirectory,
                        temporaryPath,
                        LinuxRenameExchange);
                if (rollbackResult == 0)
                {
                    throw new IOException(
                        "The Unix replacement could not be completed; the original destination was restored.",
                        commitException);
                }

                var rollbackError = new Win32Exception(Marshal.GetLastWin32Error());
                throw new HomeAssistantAtomicCommitException(
                    "The Unix replacement could not be completed or rolled back. "
                    + "The displaced original remains at the temporary path and was preserved for recovery.",
                    new AggregateException(commitException, rollbackError),
                    preserveTemporaryFile: true);
            }
            return;
        }

        var exchangeError = Marshal.GetLastWin32Error();
        if (exchangeError == UnixNoEntry)
        {
            try
            {
                File.Move(temporaryPath, destinationPath);
                return;
            }
            catch (IOException exception) when (File.Exists(destinationPath))
            {
                throw new IOException(
                    "The destination Unix file appeared while the atomic replacement was being committed; retry the export.",
                    exception);
            }
        }

        throw new IOException(
            "The Unix destination could not be exchanged atomically.",
            new Win32Exception(exchangeError));
    }

    private static void PreserveUnixDestinationMetadata(
        string destinationPath,
        string temporaryPath,
        bool useManagedApis)
    {
        var metadata = TryReadUnixFileMetadata(destinationPath);
        if (!metadata.HasValue) return;
        ApplyUnixDestinationMetadata(destinationPath, temporaryPath, metadata.Value, useManagedApis);
    }

    private static void ApplyUnixDestinationMetadata(
        string destinationPath,
        string temporaryPath,
        UnixFileMetadata metadata,
        bool useManagedApis)
    {
        if (metadata.IsSymbolicLink) return;

        // Keep the publicly named staging inode inaccessible while ownership is
        // changing. Transfer ownership before applying the destination ACL so an
        // ACL update can never expose the exchanged inode under the exporter identity.
        // Applying an ACL can update POSIX mode bits, so expose the final mode last.
        SetUnixMode(temporaryPath, 0, useManagedApis: false);

        if (Chown(temporaryPath, metadata.UserId, metadata.GroupId) != 0)
        {
            throw new IOException(
                "The temporary Unix file ownership could not be preserved.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ApplyLinuxAccessAcl(temporaryPath, metadata.LinuxAccessAcl);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ApplyMacAccessAcl(
                temporaryPath,
                metadata.MacAccessAcl
                    ?? throw new IOException("The snapshotted macOS access ACL was unavailable."));
        }

        SetUnixMode(temporaryPath, 0, useManagedApis: false);
        SetUnixMode(temporaryPath, metadata.Mode, useManagedApis);
    }

    private static void SetUnixMode(string path, uint mode, bool useManagedApis)
    {
        if (!useManagedApis || !TrySetUnixModeWithManagedApi(path, mode))
        {
            if (Chmod(path, mode) != 0)
            {
                throw new IOException(
                    "The temporary Unix file mode could not be preserved.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
    }

    private static FileStream CreateSecureUnixTemporaryFileStream(string temporaryPath)
    {
        var flags = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? MacWriteOnly | MacCreate | MacExclusive | MacCloseOnExec
            : LinuxWriteOnly | LinuxCreate | LinuxExclusive | LinuxCloseOnExec;
        var descriptor = Open(temporaryPath, flags, OwnerReadWriteMode);
        if (descriptor < 0)
        {
            throw new IOException(
                "The secure temporary file could not be created.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        var handle = new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
        try
        {
            return new FileStream(handle, FileAccess.Write, 81920, isAsync: false);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static UnixFileMetadata? TryReadUnixFileMetadata(string path)
    {
        try
        {
            return ReadUnixFileMetadata(path);
        }
        catch (IOException exception) when (
            exception.InnerException is Win32Exception native
            && native.NativeErrorCode == UnixNoEntry)
        {
            return null;
        }
    }

    private static UnixFileMetadata ReadUnixFileMetadata(string path)
    {
        var offsets = UnixMetadataOffsets();
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            for (var offset = 0; offset < 512; offset += sizeof(long))
            {
                Marshal.WriteInt64(buffer, offset, 0L);
            }

            if (LStat(path, buffer) != 0)
            {
                throw new IOException(
                    "The destination Unix file metadata could not be read.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            var rawMode = Marshal.ReadInt32(buffer, offsets.Mode);
            var isSymbolicLink = (rawMode & UnixFileTypeBits) == UnixSymbolicLink;
            var linuxAccessAcl = RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !isSymbolicLink
                ? ReadLinuxAccessAcl(path)
                : null;
            var macAccessAcl = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && !isSymbolicLink
                ? ReadMacAccessAcl(path)
                : null;
            return new UnixFileMetadata(
                ReadNativeUnsigned(buffer, offsets.Device, offsets.DeviceIs32Bit),
                ReadNativeUnsigned(buffer, offsets.Inode, offsets.InodeIs32Bit),
                unchecked((uint)(rawMode & SafeReplacementModeBits)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.UserId)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.GroupId)),
                isSymbolicLink,
                linuxAccessAcl,
                macAccessAcl);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static ulong ReadNativeUnsigned(IntPtr buffer, int offset, bool is32Bit)
        => is32Bit
            ? unchecked((uint)Marshal.ReadInt32(buffer, offset))
            : unchecked((ulong)Marshal.ReadInt64(buffer, offset));

    private static bool TrySetUnixModeWithManagedApi(string path, uint mode)
    {
        var getMode = typeof(File).GetMethod(
            "GetUnixFileMode",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string) },
            modifiers: null);
        if (getMode is null) return false;
        var modeType = getMode.ReturnType;
        var setMode = typeof(File).GetMethod(
            "SetUnixFileMode",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(string), modeType },
            modifiers: null);
        if (setMode is null) return false;
        setMode.Invoke(null, new[] { (object)path, Enum.ToObject(modeType, mode) });
        return true;
    }

    private static byte[]? ReadLinuxAccessAcl(string sourcePath)
    {
        var size = GetExtendedAttribute(sourcePath, LinuxAccessAclAttribute, IntPtr.Zero, UIntPtr.Zero).ToInt64();
        if (size < 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == LinuxNoData || error == LinuxOperationNotSupported) return null;
            throw new IOException("The destination Unix access ACL could not be read.", new Win32Exception(error));
        }
        if (size == 0) return Array.Empty<byte>();

        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            var read = GetExtendedAttribute(
                sourcePath,
                LinuxAccessAclAttribute,
                buffer,
                new UIntPtr(unchecked((ulong)size))).ToInt64();
            if (read != size)
            {
                throw new IOException(
                    "The destination Unix access ACL changed while it was being read.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
            var result = new byte[checked((int)size)];
            Marshal.Copy(buffer, result, 0, result.Length);
            return result;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void ApplyLinuxAccessAcl(string destinationPath, byte[]? accessAcl)
    {
        if (accessAcl is null)
        {
            if (RemoveExtendedAttribute(destinationPath, LinuxAccessAclAttribute) != 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != LinuxNoData && error != LinuxOperationNotSupported)
                {
                    throw new IOException(
                        "The temporary Unix access ACL could not be cleared.",
                        new Win32Exception(error));
                }
            }
            return;
        }

        var buffer = Marshal.AllocHGlobal(accessAcl.Length);
        try
        {
            if (accessAcl.Length > 0) Marshal.Copy(accessAcl, 0, buffer, accessAcl.Length);
            if (SetExtendedAttribute(
                    destinationPath,
                    LinuxAccessAclAttribute,
                    buffer,
                    new UIntPtr(unchecked((ulong)accessAcl.Length)),
                    0) != 0)
            {
                throw new IOException(
                    "The temporary Unix access ACL could not be preserved.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ReadMacAccessAcl(string sourcePath)
    {
        var acl = AclGetFile(sourcePath, MacExtendedAcl);
        if (acl == IntPtr.Zero)
        {
            throw new IOException(
                "The destination macOS access ACL could not be read.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
        try
        {
            var text = AclToText(acl, out _);
            if (text == IntPtr.Zero)
            {
                throw new IOException(
                    "The destination macOS access ACL could not be snapshotted.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
            try
            {
                return Marshal.PtrToStringAnsi(text) ?? string.Empty;
            }
            finally
            {
                AclFree(text);
            }
        }
        finally
        {
            AclFree(acl);
        }
    }

    private static void ApplyMacAccessAcl(string destinationPath, string accessAcl)
    {
        var acl = AclFromText(accessAcl);
        if (acl == IntPtr.Zero)
        {
            throw new IOException(
                "The snapshotted macOS access ACL could not be decoded.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
        try
        {
            if (AclSetFile(destinationPath, MacExtendedAcl, acl) != 0)
            {
                throw new IOException(
                    "The temporary macOS access ACL could not be preserved.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
        finally
        {
            AclFree(acl);
        }
    }

    private static UnixMetadataOffset UnixMetadataOffsets()
    {
        var operatingSystem = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "OSX"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : string.Empty;
        return UnixMetadataOffsets(operatingSystem, RuntimeInformation.ProcessArchitecture.ToString());
    }

    internal static UnixMetadataOffset UnixMetadataOffsets(string operatingSystem, string architecture)
    {
        if (string.Equals(operatingSystem, "OSX", StringComparison.Ordinal))
            return new UnixMetadataOffset(0, 8, 4, 16, 20, deviceIs32Bit: true, inodeIs32Bit: false);
        if (!string.Equals(operatingSystem, "Linux", StringComparison.Ordinal))
            throw new PlatformNotSupportedException("Unix metadata preservation supports Linux and macOS.");

        return architecture switch
        {
            "X64" or "S390x" or "Ppc64le" =>
                new UnixMetadataOffset(0, 8, 24, 28, 32, deviceIs32Bit: false, inodeIs32Bit: false),
            "X86" or "Arm" or "Armv6" =>
                new UnixMetadataOffset(0, 12, 16, 24, 28, deviceIs32Bit: false, inodeIs32Bit: true),
            "Arm64" or "LoongArch64" or "RiscV64" =>
                new UnixMetadataOffset(0, 8, 16, 24, 28, deviceIs32Bit: false, inodeIs32Bit: false),
            _ => throw new PlatformNotSupportedException(
                "Unix metadata preservation does not recognize the current Linux architecture.")
        };
    }

    internal readonly struct UnixMetadataOffset
    {
        internal UnixMetadataOffset(
            int device,
            int inode,
            int mode,
            int userId,
            int groupId,
            bool deviceIs32Bit,
            bool inodeIs32Bit)
        {
            Device = device;
            Inode = inode;
            Mode = mode;
            UserId = userId;
            GroupId = groupId;
            DeviceIs32Bit = deviceIs32Bit;
            InodeIs32Bit = inodeIs32Bit;
        }

        internal int Device { get; }
        internal int Inode { get; }
        internal int Mode { get; }
        internal int UserId { get; }
        internal int GroupId { get; }
        internal bool DeviceIs32Bit { get; }
        internal bool InodeIs32Bit { get; }
    }

    private readonly struct UnixFileMetadata
    {
        internal UnixFileMetadata(
            ulong device,
            ulong inode,
            uint mode,
            uint userId,
            uint groupId,
            bool isSymbolicLink,
            byte[]? linuxAccessAcl,
            string? macAccessAcl)
        {
            Device = device;
            Inode = inode;
            Mode = mode;
            UserId = userId;
            GroupId = groupId;
            IsSymbolicLink = isSymbolicLink;
            LinuxAccessAcl = linuxAccessAcl;
            MacAccessAcl = macAccessAcl;
        }

        internal ulong Device { get; }
        internal ulong Inode { get; }
        internal uint Mode { get; }
        internal uint UserId { get; }
        internal uint GroupId { get; }
        internal bool IsSymbolicLink { get; }
        internal byte[]? LinuxAccessAcl { get; }
        internal string? MacAccessAcl { get; }

        internal static bool SameIdentityAndPermissions(
            UnixFileMetadata? expected,
            UnixFileMetadata? current)
        {
            if (!expected.HasValue || !current.HasValue)
            {
                return expected.HasValue == current.HasValue;
            }

            return expected.Value.Device == current.Value.Device
                && expected.Value.Inode == current.Value.Inode
                && expected.Value.Mode == current.Value.Mode
                && expected.Value.UserId == current.Value.UserId
                && expected.Value.GroupId == current.Value.GroupId
                && expected.Value.IsSymbolicLink == current.Value.IsSymbolicLink
                && EqualBytes(expected.Value.LinuxAccessAcl, current.Value.LinuxAccessAcl)
                && string.Equals(expected.Value.MacAccessAcl, current.Value.MacAccessAcl, StringComparison.Ordinal);
        }

        private static bool EqualBytes(byte[]? left, byte[]? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null || left.Length != right.Length) return false;
            for (var index = 0; index < left.Length; index++)
            {
                if (left[index] != right[index]) return false;
            }
            return true;
        }
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags, uint mode);

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
    private static extern int LStat(string path, IntPtr buffer);

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);

    [DllImport("libc", EntryPoint = "chown", SetLastError = true)]
    private static extern int Chown(string path, uint owner, uint group);

    [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
    private static extern int Rename(string oldPath, string newPath);

    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
    private static extern int RenameLinux(
        int oldDirectory,
        string oldPath,
        int newDirectory,
        string newPath,
        uint flags);

    [DllImport("libc", EntryPoint = "renamex_np", SetLastError = true)]
    private static extern int RenameMac(string oldPath, string newPath, uint flags);

    [DllImport("libc", EntryPoint = "getxattr", SetLastError = true)]
    private static extern IntPtr GetExtendedAttribute(
        string path,
        string name,
        IntPtr value,
        UIntPtr size);

    [DllImport("libc", EntryPoint = "setxattr", SetLastError = true)]
    private static extern int SetExtendedAttribute(
        string path,
        string name,
        IntPtr value,
        UIntPtr size,
        int flags);

    [DllImport("libc", EntryPoint = "removexattr", SetLastError = true)]
    private static extern int RemoveExtendedAttribute(string path, string name);

    [DllImport("libc", EntryPoint = "acl_get_file", SetLastError = true)]
    private static extern IntPtr AclGetFile(string path, int type);

    [DllImport("libc", EntryPoint = "acl_set_file", SetLastError = true)]
    private static extern int AclSetFile(string path, int type, IntPtr acl);

    [DllImport("libc", EntryPoint = "acl_to_text", SetLastError = true)]
    private static extern IntPtr AclToText(IntPtr acl, out IntPtr length);

    [DllImport("libc", EntryPoint = "acl_from_text", SetLastError = true)]
    private static extern IntPtr AclFromText(string text);

    [DllImport("libc", EntryPoint = "acl_free", SetLastError = true)]
    private static extern int AclFree(IntPtr acl);
}
