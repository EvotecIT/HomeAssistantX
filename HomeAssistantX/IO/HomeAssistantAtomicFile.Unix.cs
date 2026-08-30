using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    // Preserve ordinary permissions and the sticky bit, but never restore
    // setuid/setgid onto an inode containing newly exported bytes.
    private const int SafeReplacementModeBits = 0x03FF;
    private const int UnixFileTypeBits = 0xF000;
    private const int UnixRegularFile = 0x8000;
    private const int UnixSymbolicLink = 0xA000;
    private const string LinuxAccessAclAttribute = "system.posix_acl_access";
    private const int UnixNoEntry = 2;
    private const int LinuxNoData = 61;
    private const int LinuxOperationNotSupported = 95;
    private const int MacExtendedAcl = 0x00000100;
    private const uint OwnerReadWriteMode = 0x180;
    private const int LinuxWriteOnly = 0x0001;
    private const int LinuxReadOnly = 0x0000;
    private const int LinuxCreate = 0x0040;
    private const int LinuxExclusive = 0x0080;
    private const int LinuxNoFollow = 0x20000;
    private const int LinuxCloseOnExec = 0x80000;
    private const int LinuxNonBlocking = 0x0800;
    private const int MacWriteOnly = 0x0001;
    private const int MacReadOnly = 0x0000;
    private const int MacCreate = 0x0200;
    private const int MacExclusive = 0x0800;
    private const int MacNoFollow = 0x0100;
    private const int MacCloseOnExec = 0x1000000;
    private const int MacNonBlocking = 0x0004;
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

        using var replacementHandle = OpenPinnedUnixFile(temporaryPath);
        var replacementIdentity = ReadUnixFileMetadata(replacementHandle, includeAccessAcl: false);
        RequireRegularUnixFile(replacementIdentity, "The Unix replacement must be a regular file.");

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
                var displacedIdentity = ReadUnixFileMetadata(temporaryPath, includeAccessAcl: false);
                if (displacedIdentity.IsSymbolicLink)
                {
                    RequireUnixPathIdentity(temporaryPath, displacedIdentity);
                }
                else
                {
                    RequireRegularUnixFile(
                        displacedIdentity,
                        "The Unix destination must be a regular file or symbolic link.");
                    using var displacedHandle = OpenPinnedUnixFile(temporaryPath);
                    var displaced = ReadUnixFileMetadata(displacedHandle, includeAccessAcl: true);
                    RequireRegularUnixFile(
                        displaced,
                        "The pinned Unix destination must be a regular file.");
                    if (!UnixFileMetadata.SameIdentity(displacedIdentity, displaced))
                    {
                        throw new IOException(
                            "The displaced Unix destination changed before its metadata could be pinned.");
                    }

                    RequireUnixPathIdentity(temporaryPath, displaced);
                    ApplyUnixDestinationMetadata(replacementHandle, displaced);
                    RequireUnixPathIdentity(temporaryPath, displaced);
                }

                RequireUnixPathIdentity(destinationPath, replacementIdentity);
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
        _ = useManagedApis;
        var sourceIdentity = TryReadUnixFileMetadata(destinationPath, includeAccessAcl: false);
        if (!sourceIdentity.HasValue || sourceIdentity.Value.IsSymbolicLink) return;
        RequireRegularUnixFile(
            sourceIdentity.Value,
            "The Unix destination must be a regular file or symbolic link.");

        using var sourceHandle = OpenPinnedUnixFile(destinationPath);
        var metadata = ReadUnixFileMetadata(sourceHandle, includeAccessAcl: true);
        RequireRegularUnixFile(metadata, "The pinned Unix destination must be a regular file.");
        if (!UnixFileMetadata.SameIdentity(sourceIdentity.Value, metadata))
        {
            throw new IOException(
                "The Unix destination changed before its metadata could be pinned.");
        }
        RequireUnixPathIdentity(destinationPath, metadata);

        using var temporaryHandle = OpenPinnedUnixFile(temporaryPath);
        var temporaryIdentity = ReadUnixFileMetadata(temporaryHandle, includeAccessAcl: false);
        RequireRegularUnixFile(temporaryIdentity, "The Unix replacement must be a regular file.");
        ApplyUnixDestinationMetadata(temporaryHandle, metadata);
        RequireUnixPathIdentity(temporaryPath, temporaryIdentity);
    }

    private static void ApplyUnixDestinationMetadata(
        SafeFileHandle temporaryHandle,
        UnixFileMetadata metadata)
    {
        if (metadata.IsSymbolicLink) return;

        // Keep the publicly named staging inode inaccessible while ownership is
        // changing. Transfer ownership before applying the destination ACL so an
        // ACL update can never expose the exchanged inode under the exporter identity.
        // Applying an ACL can update POSIX mode bits, so expose the final mode last.
        SetUnixMode(temporaryHandle, 0);

        if (FChown(temporaryHandle, metadata.UserId, metadata.GroupId) != 0)
        {
            throw new IOException(
                "The temporary Unix file ownership could not be preserved.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ApplyLinuxAccessAcl(temporaryHandle, metadata.LinuxAccessAcl);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ApplyMacAccessAcl(
                temporaryHandle,
                metadata.MacAccessAcl
                    ?? throw new IOException("The snapshotted macOS access ACL was unavailable."));
        }

        SetUnixMode(temporaryHandle, 0);
        SetUnixMode(temporaryHandle, metadata.Mode);
    }

    private static void SetUnixMode(SafeFileHandle handle, uint mode)
    {
        if (FChmod(handle, mode) != 0)
        {
            throw new IOException(
                "The temporary Unix file mode could not be preserved.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private static SafeFileHandle OpenPinnedUnixFile(string path)
    {
        var flags = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? MacReadOnly | MacNoFollow | MacCloseOnExec | MacNonBlocking
            : LinuxReadOnly | LinuxNoFollow | LinuxCloseOnExec | LinuxNonBlocking;
        var descriptor = EnsureUsableUnixDescriptor(Open(path, flags, 0));
        if (descriptor < 0)
        {
            throw new IOException(
                "The Unix file could not be pinned without following symbolic links.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        return new SafeFileHandle(new IntPtr(descriptor), ownsHandle: true);
    }

    private static FileStream CreateSecureUnixTemporaryFileStream(string temporaryPath)
    {
        var flags = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? MacWriteOnly | MacCreate | MacExclusive | MacCloseOnExec
            : LinuxWriteOnly | LinuxCreate | LinuxExclusive | LinuxCloseOnExec;
        var descriptor = EnsureUsableUnixDescriptor(Open(temporaryPath, flags, OwnerReadWriteMode));
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

    private static UnixFileMetadata? TryReadUnixFileMetadata(
        string path,
        bool includeAccessAcl = true)
    {
        try
        {
            return ReadUnixFileMetadata(path, includeAccessAcl);
        }
        catch (IOException exception) when (
            exception.InnerException is Win32Exception native
            && native.NativeErrorCode == UnixNoEntry)
        {
            return null;
        }
    }

    private static UnixFileMetadata ReadUnixFileMetadata(
        string path,
        bool includeAccessAcl = true)
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
            var fileType = rawMode & UnixFileTypeBits;
            var isRegularFile = fileType == UnixRegularFile;
            var linuxAccessAcl = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && isRegularFile
                ? ReadLinuxAccessAcl(path)
                : null;
            var macAccessAcl = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                && isRegularFile
                ? ReadMacAccessAcl(path)
                : null;
            return new UnixFileMetadata(
                ReadNativeUnsigned(buffer, offsets.Device, offsets.DeviceIs32Bit),
                ReadNativeUnsigned(buffer, offsets.Inode, offsets.InodeIs32Bit),
                unchecked((uint)(rawMode & SafeReplacementModeBits)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.UserId)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.GroupId)),
                unchecked((uint)fileType),
                linuxAccessAcl,
                macAccessAcl);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static UnixFileMetadata ReadUnixFileMetadata(
        SafeFileHandle handle,
        bool includeAccessAcl)
    {
        var offsets = UnixMetadataOffsets();
        var buffer = Marshal.AllocHGlobal(512);
        try
        {
            for (var offset = 0; offset < 512; offset += sizeof(long))
            {
                Marshal.WriteInt64(buffer, offset, 0L);
            }

            if (FStat(handle, buffer) != 0)
            {
                throw new IOException(
                    "The pinned Unix file metadata could not be read.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            var rawMode = Marshal.ReadInt32(buffer, offsets.Mode);
            var fileType = rawMode & UnixFileTypeBits;
            var isRegularFile = fileType == UnixRegularFile;
            var linuxAccessAcl = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                && isRegularFile
                    ? ReadLinuxAccessAcl(handle)
                    : null;
            var macAccessAcl = includeAccessAcl
                && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                && isRegularFile
                    ? ReadMacAccessAcl(handle)
                    : null;
            return new UnixFileMetadata(
                ReadNativeUnsigned(buffer, offsets.Device, offsets.DeviceIs32Bit),
                ReadNativeUnsigned(buffer, offsets.Inode, offsets.InodeIs32Bit),
                unchecked((uint)(rawMode & SafeReplacementModeBits)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.UserId)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.GroupId)),
                unchecked((uint)fileType),
                linuxAccessAcl,
                macAccessAcl);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static void RequireUnixPathIdentity(
        string path,
        UnixFileMetadata expected)
    {
        var current = ReadUnixFileMetadata(path, includeAccessAcl: false);
        if (!UnixFileMetadata.SameIdentity(expected, current))
        {
            throw new IOException(
                "The Unix file path no longer names the pinned inode.");
        }
    }

    private static void RequireRegularUnixFile(UnixFileMetadata metadata, string message)
    {
        if (!metadata.IsRegularFile) throw new IOException(message);
    }

    private static int EnsureUsableUnixDescriptor(int descriptor)
        => EnsureUsableUnixDescriptor(descriptor, DuplicateDescriptor, CloseDescriptor);

    internal static int EnsureUsableUnixDescriptor(
        int descriptor,
        Func<int, int> duplicate,
        Func<int, int> close)
    {
        if (descriptor != 0) return descriptor;
        var duplicated = duplicate(descriptor);
        if (duplicated < 0)
        {
            var error = Marshal.GetLastWin32Error();
            _ = close(descriptor);
            throw new IOException(
                "Unix descriptor zero could not be duplicated into a safe handle.",
                new Win32Exception(error));
        }

        if (close(descriptor) == 0) return duplicated;
        var closeError = Marshal.GetLastWin32Error();
        _ = close(duplicated);
        throw new IOException(
            "Unix descriptor zero could not be closed after duplication.",
            new Win32Exception(closeError));
    }

    private static ulong ReadNativeUnsigned(IntPtr buffer, int offset, bool is32Bit)
        => is32Bit
            ? unchecked((uint)Marshal.ReadInt32(buffer, offset))
            : unchecked((ulong)Marshal.ReadInt64(buffer, offset));

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
            uint fileType,
            byte[]? linuxAccessAcl,
            string? macAccessAcl)
        {
            Device = device;
            Inode = inode;
            Mode = mode;
            UserId = userId;
            GroupId = groupId;
            FileType = fileType;
            LinuxAccessAcl = linuxAccessAcl;
            MacAccessAcl = macAccessAcl;
        }

        internal ulong Device { get; }
        internal ulong Inode { get; }
        internal uint Mode { get; }
        internal uint UserId { get; }
        internal uint GroupId { get; }
        internal uint FileType { get; }
        internal bool IsRegularFile => FileType == UnixRegularFile;
        internal bool IsSymbolicLink => FileType == UnixSymbolicLink;
        internal byte[]? LinuxAccessAcl { get; }
        internal string? MacAccessAcl { get; }

        internal static bool SameIdentity(
            UnixFileMetadata left,
            UnixFileMetadata right)
            => left.Device == right.Device
                && left.Inode == right.Inode
                && left.FileType == right.FileType;

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
                && expected.Value.FileType == current.Value.FileType
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

    [DllImport("libc", EntryPoint = "dup", SetLastError = true)]
    private static extern int DuplicateDescriptor(int descriptor);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int CloseDescriptor(int descriptor);

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
    private static extern int LStat(string path, IntPtr buffer);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStat(SafeFileHandle handle, IntPtr buffer);

    [DllImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    private static extern int FChmod(SafeFileHandle handle, uint mode);

    [DllImport("libc", EntryPoint = "fchown", SetLastError = true)]
    private static extern int FChown(SafeFileHandle handle, uint owner, uint group);

    [DllImport("libc", EntryPoint = "renameat2", SetLastError = true)]
    private static extern int RenameLinux(
        int oldDirectory,
        string oldPath,
        int newDirectory,
        string newPath,
        uint flags);

    [DllImport("libc", EntryPoint = "renamex_np", SetLastError = true)]
    private static extern int RenameMac(string oldPath, string newPath, uint flags);

}
