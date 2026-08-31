using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    private const int DaclSecurityInformation = 0x00000004;
    private const int OwnerSecurityInformation = 0x00000001;
    private const int GroupSecurityInformation = 0x00000002;
    private const int LabelSecurityInformation = 0x00000010;
    private const int PreservedWindowsSecurityInformation =
        OwnerSecurityInformation | GroupSecurityInformation | DaclSecurityInformation | LabelSecurityInformation;
    private const uint WindowsCreateNew = 1;
    private const uint WindowsFileAttributeNormal = 0x00000080;
    private const uint WindowsFileFlagBackupSemantics = 0x02000000;
    private const uint WindowsFileFlagOpenReparsePoint = 0x00200000;
    private const uint WindowsFileFlagOverlapped = 0x40000000;
    private const uint WindowsGenericWrite = 0x40000000;
    private const uint WindowsFileReadAttributes = 0x00000080;
    private const uint WindowsReadControl = 0x00020000;
    private const uint WindowsDelete = 0x00010000;
    private const uint WindowsWriteDac = 0x00040000;
    private const uint WindowsWriteOwner = 0x00080000;
    private const uint WindowsFileShareRead = 0x00000001;
    private const uint WindowsFileShareWrite = 0x00000002;
    private const uint WindowsFileShareDelete = 0x00000004;
    private const uint WindowsOpenExisting = 3;
    private const int WindowsFileNotFound = 2;
    private const int WindowsPathNotFound = 3;
    private const int WindowsInsufficientBuffer = 122;
    private const ushort SecurityDescriptorDaclProtected = 0x1000;
    private const string RestrictiveTemporaryFileSddl = "D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;FA;;;OW)";
    private const int WindowsFileExists = 80;
    private const int WindowsAlreadyExists = 183;
    private const int WindowsFileRenameInfo = 3;
    private const int WindowsFileDispositionInfo = 4;
    private const int WindowsFileRenameInfoEx = 22;
    private const uint WindowsFileRenameReplaceIfExists = 0x00000001;
    private const uint WindowsFileRenamePosixSemantics = 0x00000002;
    private const uint WindowsFileAttributeEncrypted = 0x00004000;

    private static FileStream CreateSecureWindowsTemporaryFileStream(
        string temporaryPath,
        bool encrypted)
    {
        var handle = CreateFile(
            temporaryPath,
            WindowsGenericWrite | WindowsWriteDac | WindowsWriteOwner | WindowsDelete,
            0,
            IntPtr.Zero,
            WindowsCreateNew,
            WindowsFileAttributeNormal | WindowsFileFlagOverlapped
                | (encrypted ? WindowsFileAttributeEncrypted : 0),
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

        using var destinationSecurity = overwrite
            ? TryCaptureWindowsDestinationSecurity(destinationPath)
            : null;
        var inheritedDestinationSecurity = false;
        var committed = false;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (destinationSecurity is not null)
            {
                if (destinationSecurity.IsEncrypted
                    != IsWindowsFileEncrypted(temporaryStream.SafeFileHandle))
                {
                    throw new IOException(
                        "The Windows replacement encryption state no longer matches the pinned destination.");
                }

                if (!SetKernelObjectSecurity(
                        temporaryStream.SafeFileHandle,
                        PreservedWindowsSecurityInformation,
                        destinationSecurity.Descriptor))
                {
                    throw new IOException(
                        "The Windows destination owner or access control list could not be preserved on the pinned replacement.",
                        new Win32Exception(Marshal.GetLastWin32Error()));
                }
                inheritedDestinationSecurity = true;
                cancellationToken.ThrowIfCancellationRequested();
            }

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
                    Flags = GetWindowsRenameFlags(overwrite, destinationSecurity is not null),
                    RootDirectory = IntPtr.Zero,
                    FileNameLength = checked((uint)fileNameBytes),
                    FileName = '\0'
                };
                Marshal.StructureToPtr(information, buffer, fDeleteOld: false);
                Marshal.Copy(fullDestinationPath.ToCharArray(), 0, IntPtr.Add(buffer, fileNameOffset), fullDestinationPath.Length);
                Marshal.WriteInt16(buffer, fileNameOffset + fileNameBytes, 0);

                if (SetFileInformationByHandle(
                        temporaryStream.SafeFileHandle,
                        GetWindowsRenameInformationClass(overwrite, destinationSecurity is not null),
                        buffer,
                        checked((uint)bufferSize)))
                {
                    committed = true;
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
        finally
        {
            if (inheritedDestinationSecurity && !committed)
            {
                RestrictOrDeleteFailedWindowsReplacement(temporaryStream);
            }
        }
    }

    private static WindowsSecurityDescriptor? TryCaptureWindowsDestinationSecurity(string destinationPath)
    {
        var handle = CreateFile(
            destinationPath,
            WindowsReadControl | WindowsFileReadAttributes,
            WindowsFileShareRead,
            IntPtr.Zero,
            WindowsOpenExisting,
            WindowsDestinationSecurityOpenFlags,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is WindowsFileNotFound or WindowsPathNotFound) return null;
            throw new IOException(
                "The existing Windows destination security descriptor could not be opened for preservation.",
                new Win32Exception(error));
        }

        try
        {
            if (GetKernelObjectSecurity(
                    handle,
                    PreservedWindowsSecurityInformation,
                    IntPtr.Zero,
                    0,
                    out var required)
                || Marshal.GetLastWin32Error() != WindowsInsufficientBuffer)
            {
                throw new IOException(
                    "The existing Windows destination security descriptor size could not be read.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            var descriptor = Marshal.AllocHGlobal(checked((int)required));
            if (GetKernelObjectSecurity(
                    handle,
                    PreservedWindowsSecurityInformation,
                    descriptor,
                    required,
                out _))
            {
                return new WindowsSecurityDescriptor(
                    descriptor,
                    handle,
                    IsWindowsFileEncrypted(handle));
            }

            var error = Marshal.GetLastWin32Error();
            Marshal.FreeHGlobal(descriptor);
            throw new IOException(
                "The existing Windows destination security descriptor could not be captured.",
                new Win32Exception(error));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    internal static uint WindowsDestinationSecurityOpenFlags =>
        WindowsFileAttributeNormal | WindowsFileFlagBackupSemantics | WindowsFileFlagOpenReparsePoint;

    internal static uint WindowsDestinationSecurityShareMode =>
        WindowsFileShareRead;

    internal static int WindowsAtomicRenameInformationClass => WindowsFileRenameInfoEx;

    internal static uint WindowsAtomicRenameFlags =>
        WindowsFileRenameReplaceIfExists | WindowsFileRenamePosixSemantics;

    internal static int WindowsPreservedSecurityInformation =>
        PreservedWindowsSecurityInformation;

    internal static int WindowsMandatoryLabelSecurityInformation =>
        LabelSecurityInformation;

    internal static uint WindowsEncryptedAttribute =>
        WindowsFileAttributeEncrypted;

    internal static string ReadWindowsMandatoryLabel(FileStream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (GetKernelObjectSecurity(
                stream.SafeFileHandle,
                LabelSecurityInformation,
                IntPtr.Zero,
                0,
                out var required)
            || Marshal.GetLastWin32Error() != WindowsInsufficientBuffer)
        {
            throw new IOException(
                "The Windows mandatory integrity label size could not be read.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        var descriptor = Marshal.AllocHGlobal(checked((int)required));
        try
        {
            if (!GetKernelObjectSecurity(
                    stream.SafeFileHandle,
                    LabelSecurityInformation,
                    descriptor,
                    required,
                    out _))
            {
                throw new IOException(
                    "The Windows mandatory integrity label could not be read.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            if (!ConvertSecurityDescriptorToStringSecurityDescriptor(
                    descriptor,
                    1,
                    LabelSecurityInformation,
                    out var sddl,
                    out _))
            {
                throw new IOException(
                    "The Windows mandatory integrity label could not be rendered.",
                    new Win32Exception(Marshal.GetLastWin32Error()));
            }

            try
            {
                var rendered = Marshal.PtrToStringUni(sddl) ?? string.Empty;
                var aceIndex = rendered.IndexOf('(');
                return aceIndex < 0 ? rendered : "S:" + rendered.Substring(aceIndex);
            }
            finally
            {
                LocalFree(sddl);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(descriptor);
        }
    }

    internal static bool ShouldEncryptWindowsReplacement(
        string destinationPath,
        bool overwrite)
    {
        if (!overwrite) return false;
        var handle = CreateFile(
            destinationPath,
            WindowsFileReadAttributes,
            WindowsFileShareRead,
            IntPtr.Zero,
            WindowsOpenExisting,
            WindowsDestinationSecurityOpenFlags,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            if (error is WindowsFileNotFound or WindowsPathNotFound) return false;
            throw new IOException(
                "The existing Windows destination attributes could not be opened for encryption preservation.",
                new Win32Exception(error));
        }

        using (handle)
        {
            return IsWindowsFileEncrypted(handle);
        }
    }

    internal static int GetWindowsRenameInformationClass(bool overwrite, bool destinationPinned)
        => overwrite && destinationPinned ? WindowsFileRenameInfoEx : WindowsFileRenameInfo;

    internal static uint GetWindowsRenameFlags(bool overwrite, bool destinationPinned)
        => overwrite && destinationPinned ? WindowsAtomicRenameFlags : 0;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WindowsFileRenameInformation
    {
        internal uint Flags;
        internal IntPtr RootDirectory;
        internal uint FileNameLength;
        internal char FileName;
    }

    private sealed class WindowsSecurityDescriptor : IDisposable
    {
        private SafeFileHandle? _destinationHandle;

        internal WindowsSecurityDescriptor(
            IntPtr descriptor,
            SafeFileHandle destinationHandle,
            bool isEncrypted)
        {
            Descriptor = descriptor;
            _destinationHandle = destinationHandle;
            IsEncrypted = isEncrypted;
        }

        internal IntPtr Descriptor { get; private set; }
        internal bool IsEncrypted { get; }

        public void Dispose()
        {
            if (Descriptor != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Descriptor);
                Descriptor = IntPtr.Zero;
            }
            _destinationHandle?.Dispose();
            _destinationHandle = null;
        }
    }

    private static bool IsWindowsFileEncrypted(SafeFileHandle handle)
    {
        if (!GetFileInformationByHandleEx(
                handle,
                0,
                out var information,
                checked((uint)Marshal.SizeOf<WindowsFileBasicInformation>())))
        {
            throw new IOException(
                "The existing Windows destination attributes could not be read.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        return (information.FileAttributes & WindowsFileAttributeEncrypted) != 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsFileBasicInformation
    {
        internal long CreationTime;
        internal long LastAccessTime;
        internal long LastWriteTime;
        internal long ChangeTime;
        internal uint FileAttributes;
    }

    private static void RestrictOrDeleteFailedWindowsReplacement(FileStream stream)
    {
        try
        {
            ApplyRestrictiveWindowsDacl(stream);
            return;
        }
        catch (Exception restrictionFailure)
        {
            var disposition = new WindowsFileDispositionInformation { DeleteFile = true };
            var size = Marshal.SizeOf<WindowsFileDispositionInformation>();
            var buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(disposition, buffer, fDeleteOld: false);
                if (SetFileInformationByHandle(
                        stream.SafeFileHandle,
                        WindowsFileDispositionInfo,
                        buffer,
                        checked((uint)size)))
                {
                    return;
                }

                throw new IOException(
                    "The failed Windows replacement could neither be restricted nor deleted by its pinned handle.",
                    new AggregateException(restrictionFailure, new Win32Exception(Marshal.GetLastWin32Error())));
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowsFileDispositionInformation
    {
        [MarshalAs(UnmanagedType.U1)]
        internal bool DeleteFile;
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

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int fileInformationClass,
        out WindowsFileBasicInformation fileInformation,
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

    [DllImport("advapi32.dll", EntryPoint = "ConvertSecurityDescriptorToStringSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ConvertSecurityDescriptorToStringSecurityDescriptor(
        IntPtr securityDescriptor,
        uint requestedStringSecurityDescriptorRevision,
        int securityInformation,
        out IntPtr stringSecurityDescriptor,
        out uint stringSecurityDescriptorLength);

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
