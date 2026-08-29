using System.ComponentModel;
using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    private const int MoveFileReplaceExisting = 0x1;
    private const int MoveFileWriteThrough = 0x8;

    private static void CommitWindowsOverwrite(
        string temporaryPath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            File.Replace(temporaryPath, destinationPath, null);
            return;
        }
        catch (FileNotFoundException) when (File.Exists(temporaryPath))
        {
            // The destination was absent. MoveFileEx handles both a still-absent
            // destination and a file created by another process after this attempt.
        }
        catch (IOException) when (File.Exists(temporaryPath) && !File.Exists(destinationPath))
        {
            // File.Replace reports a platform-specific IOException when its
            // destination disappears. The overwrite-capable commit below is safe
            // for either state and does not branch on that stale observation.
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!MoveFileEx(
                temporaryPath,
                destinationPath,
                MoveFileReplaceExisting | MoveFileWriteThrough))
        {
            throw new IOException(
                "The temporary file could not be committed atomically.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "MoveFileExW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileEx(string existingFileName, string newFileName, int flags);
}
