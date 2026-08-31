using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    private const int DaclSecurityInformation = 0x00000004;
    private const uint WindowsCreateNew = 1;
    private const uint WindowsFileAttributeNormal = 0x00000080;
    private const uint WindowsFileFlagOverlapped = 0x40000000;
    private const uint WindowsGenericWrite = 0x40000000;
    private const uint WindowsDelete = 0x00010000;
    private const uint WindowsWriteDac = 0x00040000;
    private const int WindowsInsufficientBuffer = 122;
    private const ushort SecurityDescriptorDaclProtected = 0x1000;
    private const string RestrictiveTemporaryFileSddl = "D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;OW)";
    private const int WindowsFileExists = 80;
    private const int WindowsAlreadyExists = 183;
    private const int WindowsFileRenameInfo = 3;

    private static FileStream CreateSecureWindowsTemporaryFileStream(string temporaryPath)
    {
        var handle = CreateFile(
            temporaryPath,
            WindowsGenericWrite | WindowsWriteDac | WindowsDelete,
            0,
            IntPtr.Zero,
            WindowsCreateNew,
            WindowsFileAttributeNormal | WindowsFileFlagOverlapped,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new IOException(
                "The secure Windows temporary file could not be created.",
                new Win32Exception(error));
        }

        try
        {
            var stream = new FileStream(handle, FileAccess.Write, 81920, isAsync: true);
            try
            {
                ApplyRestrictiveWindowsDacl(stream);
                return stream;
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
        catch
        {
            handle.Dispose();
            try { File.Delete(temporaryPath); } catch { }
            throw;
        }
    }

    private static void CommitWindowsPinnedHandle(
        FileStream temporaryStream,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        if (temporaryStream is null) throw new ArgumentNullException(nameof(temporaryStream));
        cancellationToken.ThrowIfCancellationRequested();
        temporaryStream.Flush(flushToDisk: true);
        cancellationToken.ThrowIfCancellationRequested();

        var fullDestinationPath = Path.GetFullPath(destinationPath);
        var fileNameBytes = checked(fullDestinationPath.Length * sizeof(char));
        var fileNameOffset = checked((int)Marshal.OffsetOf<WindowsFileRenameInformation>(
            nameof(WindowsFileRenameInformation.FileName)));
        var bufferSize = checked(fileNameOffset + fileNameBytes + sizeof(char));
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            var information = new WindowsFileRenameInformation
            {
                ReplaceIfExists = overwrite,
                RootDirectory = IntPtr.Zero,
                FileNameLength = checked((uint)fileNameBytes),
                FileName = '\0'
            };
            Marshal.StructureToPtr(information, buffer, fDeleteOld: false);
            Marshal.Copy(fullDestinationPath.ToCharArray(), 0, IntPtr.Add(buffer, fileNameOffset), fullDestinationPath.Length);
            Marshal.WriteInt16(buffer, fileNameOffset + fileNameBytes, 0);

            if (SetFileInformationByHandle(
                    temporaryStream.SafeFileHandle,
                    WindowsFileRenameInfo,
                    buffer,
                    checked((uint)bufferSize)))
            {
                return;
            }

            var error = Marshal.GetLastWin32Error();
            if (!overwrite
                && (error == WindowsFileExists || error == WindowsAlreadyExists || File.Exists(destinationPath)))
            {
                throw new IOException(
                    "The destination file already exists. Use -Force to overwrite it.",
                    new Win32Exception(error));
            }

            throw new IOException(
                "The pinned Windows temporary file could not be committed atomically.",
                new Win32Exception(error));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowsFileRenameInformation
    {
        [MarshalAs(UnmanagedType.Bool)]
        internal bool ReplaceIfExists;
        internal IntPtr RootDirectory;
        internal uint FileNameLength;
        internal char FileName;
    }

    private static void ApplyRestrictiveWindowsDacl(FileStream stream)
    {
        if (!ConvertStringSecurityDescriptorToSecurityDescriptor(
                RestrictiveTemporaryFileSddl,
                1,
                out var descriptor,
                out _))
        {
            throw new IOException(
                "A restrictive Windows temporary-file security descriptor could not be created.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        try
        {
            if (!SetKernelObjectSecurity(stream.SafeFileHandle, DaclSecurityInformation, descriptor))
            {
                throw new IOException(
                    "The Windows temporary file could not be restricted before writing.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
        }
        finally
        {
            LocalFree(descriptor);
        }
    }

    internal static bool IsWindowsTemporaryDaclProtected(FileStream stream)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("Windows file security is available only on Windows.");

        if (GetKernelObjectSecurity(
                stream.SafeFileHandle,
                DaclSecurityInformation,
                IntPtr.Zero,
                0,
                out var required)
            || Marshal.GetLastWin32Error() != WindowsInsufficientBuffer)
        {
            throw new IOException(
                "The Windows temporary-file security descriptor size could not be read.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        var descriptor = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            if (!GetKernelObjectSecurity(
                    stream.SafeFileHandle,
                    DaclSecurityInformation,
                    descriptor,
                    required,
                    out _)
                || !GetSecurityDescriptorControl(descriptor, out var control, out _))
            {
                throw new IOException(
                    "The Windows temporary-file security descriptor could not be verified.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }
            return (control & SecurityDescriptorDaclProtected) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(descriptor);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        SafeFileHandle file,
        int fileInformationClass,
        IntPtr fileInformation,
        uint bufferSize);

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
        string stringSecurityDescriptor,
        uint stringSecurityDescriptorRevision,
        out IntPtr securityDescriptor,
        out uint securityDescriptorSize);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetKernelObjectSecurity(
        SafeHandle handle,
        int securityInformation,
        IntPtr securityDescriptor);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKernelObjectSecurity(
        SafeHandle handle,
        int securityInformation,
        IntPtr securityDescriptor,
        uint length,
        out uint lengthNeeded);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorControl(
        IntPtr securityDescriptor,
        out ushort control,
        out uint revision);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LocalFree(IntPtr memory);
}
