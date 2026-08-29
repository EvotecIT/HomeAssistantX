using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    private const int PermissionBits = 0x0FFF;
    private const string LinuxAccessAclAttribute = "system.posix_acl_access";
    private const int UnixNoEntry = 2;
    private const int LinuxNoData = 61;
    private const int LinuxOperationNotSupported = 95;
    private const int MacExtendedAcl = 0x00000100;

    private static void CommitUnixOverwrite(string temporaryPath, string destinationPath)
    {
        if (Rename(temporaryPath, destinationPath) != 0)
        {
            throw new IOException(
                "The temporary file could not be committed atomically.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    private static void PreserveUnixDestinationMetadata(
        string destinationPath,
        string temporaryPath,
        bool useManagedApis)
    {
        UnixFileMetadata metadata;
        try
        {
            metadata = ReadUnixFileMetadata(destinationPath);
        }
        catch (IOException exception) when (
            exception.InnerException is Win32Exception native
            && native.NativeErrorCode == UnixNoEntry)
        {
            return;
        }

        if (Chown(temporaryPath, metadata.UserId, metadata.GroupId) != 0)
        {
            throw new IOException(
                "The temporary Unix file ownership could not be preserved.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        if (!useManagedApis || !TrySetUnixModeWithManagedApi(temporaryPath, metadata.Mode))
        {
            if (Chmod(temporaryPath, metadata.Mode) != 0)
            {
                throw new IOException(
                    "The temporary Unix file mode could not be preserved.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            CopyLinuxAccessAcl(destinationPath, temporaryPath);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            CopyMacAccessAcl(destinationPath, temporaryPath);
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

            if (Stat(path, buffer) != 0)
            {
                throw new IOException(
                    "The destination Unix file metadata could not be read.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            return new UnixFileMetadata(
                unchecked((uint)(Marshal.ReadInt32(buffer, offsets.Mode) & PermissionBits)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.UserId)),
                unchecked((uint)Marshal.ReadInt32(buffer, offsets.GroupId)));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

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

    private static void CopyLinuxAccessAcl(string sourcePath, string destinationPath)
    {
        var size = GetExtendedAttribute(sourcePath, LinuxAccessAclAttribute, IntPtr.Zero, UIntPtr.Zero).ToInt64();
        if (size < 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == LinuxNoData || error == LinuxOperationNotSupported) return;
            throw new IOException("The destination Unix access ACL could not be read.", new Win32Exception(error));
        }
        if (size == 0) return;

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
            if (SetExtendedAttribute(
                    destinationPath,
                    LinuxAccessAclAttribute,
                    buffer,
                    new UIntPtr(unchecked((ulong)size)),
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

    private static void CopyMacAccessAcl(string sourcePath, string destinationPath)
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
            return new UnixMetadataOffset(4, 16, 20);
        if (!string.Equals(operatingSystem, "Linux", StringComparison.Ordinal))
            throw new PlatformNotSupportedException("Unix metadata preservation supports Linux and macOS.");

        return architecture switch
        {
            "X64" or "S390x" or "Ppc64le" => new UnixMetadataOffset(24, 28, 32),
            "X86" or "Arm" or "Arm64" or "Armv6" or "LoongArch64" or "RiscV64" =>
                new UnixMetadataOffset(16, 24, 28),
            _ => throw new PlatformNotSupportedException(
                "Unix metadata preservation does not recognize the current Linux architecture.")
        };
    }

    internal readonly struct UnixMetadataOffset
    {
        internal UnixMetadataOffset(int mode, int userId, int groupId)
        {
            Mode = mode;
            UserId = userId;
            GroupId = groupId;
        }

        internal int Mode { get; }
        internal int UserId { get; }
        internal int GroupId { get; }
    }

    private readonly struct UnixFileMetadata
    {
        internal UnixFileMetadata(uint mode, uint userId, uint groupId)
        {
            Mode = mode;
            UserId = userId;
            GroupId = groupId;
        }

        internal uint Mode { get; }
        internal uint UserId { get; }
        internal uint GroupId { get; }
    }

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int Stat(string path, IntPtr buffer);

    [DllImport("libc", EntryPoint = "chmod", SetLastError = true)]
    private static extern int Chmod(string path, uint mode);

    [DllImport("libc", EntryPoint = "chown", SetLastError = true)]
    private static extern int Chown(string path, uint owner, uint group);

    [DllImport("libc", EntryPoint = "rename", SetLastError = true)]
    private static extern int Rename(string oldPath, string newPath);

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

    [DllImport("libc", EntryPoint = "acl_get_file", SetLastError = true)]
    private static extern IntPtr AclGetFile(string path, int type);

    [DllImport("libc", EntryPoint = "acl_set_file", SetLastError = true)]
    private static extern int AclSetFile(string path, int type, IntPtr acl);

    [DllImport("libc", EntryPoint = "acl_free", SetLastError = true)]
    private static extern int AclFree(IntPtr acl);
}
