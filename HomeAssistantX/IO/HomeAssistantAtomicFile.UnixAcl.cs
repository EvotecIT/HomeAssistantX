using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    private static byte[]? ReadLinuxAccessAcl(string sourcePath)
        => ReadLinuxExtendedAttribute(
            sourcePath,
            LinuxAccessAclAttribute,
            "The destination Unix access ACL could not be read.");

    private static byte[]? ReadLinuxAccessAcl(
        SafeFileHandle sourceHandle,
        bool allowProcDescriptorPath)
        => ReadLinuxExtendedAttribute(
            sourceHandle,
            LinuxAccessAclAttribute,
            "The pinned Unix access ACL could not be read.",
            allowProcDescriptorPath);

    private static byte[]? ReadLinuxSecurityContext(string sourcePath)
        => ReadLinuxExtendedAttribute(
            sourcePath,
            LinuxSecurityContextAttribute,
            "The destination SELinux context could not be read.");

    private static byte[]? ReadLinuxSecurityContext(
        SafeFileHandle sourceHandle,
        bool allowProcDescriptorPath)
        => ReadLinuxExtendedAttribute(
            sourceHandle,
            LinuxSecurityContextAttribute,
            "The pinned SELinux context could not be read.",
            allowProcDescriptorPath);

    private static byte[]? ReadLinuxExtendedAttribute(
        string sourcePath,
        string attributeName,
        string failureMessage)
        => ReadLinuxExtendedAttribute(
            (value, size) => GetExtendedAttribute(sourcePath, attributeName, value, size),
            failureMessage);

    private static byte[]? ReadLinuxExtendedAttribute(
        SafeFileHandle sourceHandle,
        string attributeName,
        string failureMessage,
        bool allowProcDescriptorPath)
    {
        try
        {
            return ReadLinuxExtendedAttribute(
                (value, size) => FGetExtendedAttribute(sourceHandle, attributeName, value, size),
                failureMessage);
        }
        catch (IOException exception) when (IsNativeError(exception, UnixBadFileDescriptor))
        {
            if (allowProcDescriptorPath)
            {
                var addedReference = false;
                try
                {
                    sourceHandle.DangerousAddRef(ref addedReference);
                    var descriptorPath = "/proc/self/fd/"
                        + sourceHandle.DangerousGetHandle().ToInt64().ToString(CultureInfo.InvariantCulture);
                    return ReadLinuxExtendedAttribute(descriptorPath, attributeName, failureMessage);
                }
                catch (IOException procException) when (CanFallBackFromProcDescriptor(procException))
                {
                    // Hardened containers and chroots may omit procfs. Never fall
                    // back to the mutable pathname: metadata must remain bound to
                    // the descriptor that pinned the destination inode.
                }
                finally
                {
                    if (addedReference) sourceHandle.DangerousRelease();
                }
            }

            throw;
        }
    }

    private static byte[]? ReadLinuxExtendedAttribute(
        Func<IntPtr, UIntPtr, IntPtr> read,
        string failureMessage)
    {
        var size = read(IntPtr.Zero, UIntPtr.Zero).ToInt64();
        if (size < 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == LinuxNoData || error == LinuxOperationNotSupported) return null;
            throw new IOException(failureMessage, new Win32Exception(error));
        }
        if (size == 0) return Array.Empty<byte>();

        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            var actual = read(buffer, new UIntPtr(unchecked((ulong)size))).ToInt64();
            if (actual != size)
            {
                throw new IOException(
                    failureMessage + " The attribute changed while it was being read.",
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

    private static bool IsNativeError(IOException exception, int error)
        => exception.InnerException is Win32Exception native && native.NativeErrorCode == error;

    private static bool CanFallBackFromProcDescriptor(IOException exception)
        => exception.InnerException is Win32Exception native
            && native.NativeErrorCode is UnixNoEntry or UnixPermissionDenied or UnixOperationNotPermitted;

    private static void ApplyLinuxAccessAcl(SafeFileHandle destinationHandle, byte[]? accessAcl)
        => ApplyLinuxExtendedAttribute(
            destinationHandle,
            LinuxAccessAclAttribute,
            accessAcl,
            clearWhenMissing: true,
            "The temporary Unix access ACL could not be preserved.");

    private static void ApplyLinuxSecurityContext(
        SafeFileHandle destinationHandle,
        byte[]? securityContext)
        => ApplyLinuxExtendedAttribute(
            destinationHandle,
            LinuxSecurityContextAttribute,
            securityContext,
            clearWhenMissing: false,
            "The temporary SELinux context could not be preserved.");

    private static void ApplyLinuxExtendedAttribute(
        SafeFileHandle destinationHandle,
        string attributeName,
        byte[]? value,
        bool clearWhenMissing,
        string failureMessage)
    {
        if (value is null)
        {
            if (!clearWhenMissing) return;
            if (FRemoveExtendedAttribute(destinationHandle, attributeName) != 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != LinuxNoData && error != LinuxOperationNotSupported)
                {
                    throw new IOException(failureMessage, new Win32Exception(error));
                }
            }
            return;
        }

        var buffer = Marshal.AllocHGlobal(value.Length);
        try
        {
            if (value.Length > 0) Marshal.Copy(value, 0, buffer, value.Length);
            if (FSetExtendedAttribute(
                    destinationHandle,
                    attributeName,
                    buffer,
                    new UIntPtr(unchecked((ulong)value.Length)),
                    0) != 0)
            {
                throw new IOException(failureMessage, new Win32Exception(Marshal.GetLastWin32Error()));
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
            var error = Marshal.GetLastWin32Error();
            if (error == UnixNoEntry) return string.Empty;
            throw new IOException(
                "The destination macOS access ACL could not be read.",
                new Win32Exception(error));
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

    private static string ReadMacAccessAcl(SafeFileHandle sourceHandle)
    {
        var acl = AclGetFileDescriptor(sourceHandle, MacExtendedAcl);
        if (acl == IntPtr.Zero)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == UnixNoEntry) return string.Empty;
            throw new IOException(
                "The pinned macOS access ACL could not be read.",
                new Win32Exception(error));
        }
        try
        {
            var text = AclToText(acl, out _);
            if (text == IntPtr.Zero)
            {
                throw new IOException(
                    "The pinned macOS access ACL could not be snapshotted.",
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

    private static void ApplyMacAccessAcl(SafeFileHandle destinationHandle, string accessAcl)
    {
        if (accessAcl.Length == 0)
        {
            if (AclDeleteFileDescriptor(destinationHandle, MacExtendedAcl) != 0)
            {
                var error = Marshal.GetLastWin32Error();
                if (error != UnixNoEntry)
                {
                    throw new IOException(
                        "The temporary macOS access ACL could not be cleared.",
                        new Win32Exception(error));
                }
            }
            return;
        }

        var acl = AclFromText(accessAcl);
        if (acl == IntPtr.Zero)
        {
            throw new IOException(
                "The snapshotted macOS access ACL could not be decoded.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
        try
        {
            if (AclSetFileDescriptor(destinationHandle, acl, MacExtendedAcl) != 0)
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

    [DllImport("libc", EntryPoint = "getxattr", SetLastError = true)]
    private static extern IntPtr GetExtendedAttribute(
        string path,
        string name,
        IntPtr value,
        UIntPtr size);

    [DllImport("libc", EntryPoint = "fgetxattr", SetLastError = true)]
    private static extern IntPtr FGetExtendedAttribute(
        SafeFileHandle handle,
        string name,
        IntPtr value,
        UIntPtr size);

    [DllImport("libc", EntryPoint = "fsetxattr", SetLastError = true)]
    private static extern int FSetExtendedAttribute(
        SafeFileHandle handle,
        string name,
        IntPtr value,
        UIntPtr size,
        int flags);

    [DllImport("libc", EntryPoint = "fremovexattr", SetLastError = true)]
    private static extern int FRemoveExtendedAttribute(SafeFileHandle handle, string name);

    [DllImport("libc", EntryPoint = "acl_get_file", SetLastError = true)]
    private static extern IntPtr AclGetFile(string path, int type);

    [DllImport("libc", EntryPoint = "acl_get_fd_np", SetLastError = true)]
    private static extern IntPtr AclGetFileDescriptor(SafeFileHandle handle, int type);

    [DllImport("libc", EntryPoint = "acl_set_fd_np", SetLastError = true)]
    private static extern int AclSetFileDescriptor(SafeFileHandle handle, IntPtr acl, int type);

    [DllImport("libc", EntryPoint = "acl_delete_fd_np", SetLastError = true)]
    private static extern int AclDeleteFileDescriptor(SafeFileHandle handle, int type);

    [DllImport("libc", EntryPoint = "acl_to_text", SetLastError = true)]
    private static extern IntPtr AclToText(IntPtr acl, out IntPtr length);

    [DllImport("libc", EntryPoint = "acl_from_text", SetLastError = true)]
    private static extern IntPtr AclFromText(string text);

    [DllImport("libc", EntryPoint = "acl_free", SetLastError = true)]
    private static extern int AclFree(IntPtr acl);
}
