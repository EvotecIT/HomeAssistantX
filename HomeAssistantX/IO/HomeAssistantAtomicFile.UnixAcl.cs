using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
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

    private static byte[]? ReadLinuxAccessAcl(SafeFileHandle sourceHandle)
    {
        var size = FGetExtendedAttribute(
            sourceHandle,
            LinuxAccessAclAttribute,
            IntPtr.Zero,
            UIntPtr.Zero).ToInt64();
        if (size < 0)
        {
            var error = Marshal.GetLastWin32Error();
            if (error == LinuxNoData || error == LinuxOperationNotSupported) return null;
            throw new IOException("The pinned Unix access ACL could not be read.", new Win32Exception(error));
        }
        if (size == 0) return Array.Empty<byte>();

        var buffer = Marshal.AllocHGlobal(checked((int)size));
        try
        {
            var read = FGetExtendedAttribute(
                sourceHandle,
                LinuxAccessAclAttribute,
                buffer,
                new UIntPtr(unchecked((ulong)size))).ToInt64();
            if (read != size)
            {
                throw new IOException(
                    "The pinned Unix access ACL changed while it was being read.",
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

    private static void ApplyLinuxAccessAcl(SafeFileHandle destinationHandle, byte[]? accessAcl)
    {
        if (accessAcl is null)
        {
            if (FRemoveExtendedAttribute(destinationHandle, LinuxAccessAclAttribute) != 0)
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
            if (FSetExtendedAttribute(
                    destinationHandle,
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

    private static string ReadMacAccessAcl(SafeFileHandle sourceHandle)
    {
        var acl = AclGetFileDescriptor(sourceHandle, MacExtendedAcl);
        if (acl == IntPtr.Zero)
        {
            throw new IOException(
                "The pinned macOS access ACL could not be read.",
                new Win32Exception(Marshal.GetLastWin32Error()));
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

    [DllImport("libc", EntryPoint = "acl_to_text", SetLastError = true)]
    private static extern IntPtr AclToText(IntPtr acl, out IntPtr length);

    [DllImport("libc", EntryPoint = "acl_from_text", SetLastError = true)]
    private static extern IntPtr AclFromText(string text);

    [DllImport("libc", EntryPoint = "acl_free", SetLastError = true)]
    private static extern int AclFree(IntPtr acl);
}
