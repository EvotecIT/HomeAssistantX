using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    private const int MoveFileWriteThrough = 0x8;
    private const int WindowsFileExists = 80;
    private const int WindowsAlreadyExists = 183;
    private const int MaximumWindowsCommitRetries = 4;

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
}
