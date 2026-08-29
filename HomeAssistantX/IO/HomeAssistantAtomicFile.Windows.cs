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
    private const uint WindowsWriteDac = 0x00040000;
    private const int WindowsInsufficientBuffer = 122;
    private const ushort SecurityDescriptorDaclProtected = 0x1000;
    private const string RestrictiveTemporaryFileSddl = "D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;OW)";
    private const int MoveFileWriteThrough = 0x8;
    private const int WindowsFileExists = 80;
    private const int WindowsAlreadyExists = 183;
    private const int MaximumWindowsCommitRetries = 4;

    private static FileStream CreateSecureWindowsTemporaryFileStream(string temporaryPath)
    {
        var handle = CreateFile(
            temporaryPath,
            WindowsGenericWrite | WindowsWriteDac,
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

    private static void CommitWindowsOverwrite(
        string temporaryPath,
        string destinationPath,
        CancellationToken cancellationToken,
        Action? beforeNoReplaceMove)
    {
        for (var attempt = 0; attempt < MaximumWindowsCommitRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                File.Replace(temporaryPath, destinationPath, null);
                return;
            }
            catch (FileNotFoundException) when (File.Exists(temporaryPath))
            {
                // The destination was absent at the replace boundary. The no-replace
                // move below either commits into that absence or reports a new file.
            }
            catch (IOException) when (File.Exists(temporaryPath) && !File.Exists(destinationPath))
            {
                // File.Replace can report a platform-specific IOException when its
                // destination disappears. Never convert that observation into an
                // overwrite-capable move.
            }

            cancellationToken.ThrowIfCancellationRequested();
            beforeNoReplaceMove?.Invoke();
            beforeNoReplaceMove = null;
            if (MoveFileEx(temporaryPath, destinationPath, MoveFileWriteThrough))
            {
                return;
            }

            var error = Marshal.GetLastWin32Error();
            if ((error == WindowsFileExists || error == WindowsAlreadyExists)
                && File.Exists(temporaryPath))
            {
                continue;
            }

            throw new IOException(
                "The temporary file could not be committed atomically.",
                new Win32Exception(error));
        }

        throw new IOException(
            "The destination Windows file changed while its security metadata was being preserved.");
    }

    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);

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
