using System.Runtime.InteropServices;

namespace HomeAssistantX.IO;

internal static partial class HomeAssistantAtomicFile
{
    private const int ExportWriteChunkSize = 64 * 1024;

    internal static string CreateTemporaryPath(string directory)
    {
        if (directory is null) throw new ArgumentNullException(nameof(directory));
        return Path.Combine(directory, ".homeassistantx-" + Guid.NewGuid().ToString("N") + ".tmp");
    }

    internal static void CommitTemporaryFile(
        string temporaryPath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
        => CommitTemporaryFile(
            temporaryPath,
            destinationPath,
            overwrite,
            cancellationToken,
            beforeUnixMetadataRecheck: null,
            beforeWindowsNoReplaceMove: null);

    internal static void CommitTemporaryFile(
        string temporaryPath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken,
        Action? beforeUnixMetadataRecheck)
        => CommitTemporaryFile(
            temporaryPath,
            destinationPath,
            overwrite,
            cancellationToken,
            beforeUnixMetadataRecheck,
            beforeWindowsNoReplaceMove: null);

    internal static void CommitTemporaryFile(
        string temporaryPath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken,
        Action? beforeUnixMetadataRecheck,
        Action? beforeWindowsNoReplaceMove)
        => CommitTemporaryFile(
            temporaryPath,
            destinationPath,
            overwrite,
            cancellationToken,
            beforeUnixMetadataRecheck,
            beforeWindowsNoReplaceMove,
            afterUnixExchange: null);

    internal static void CommitTemporaryFile(
        string temporaryPath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken,
        Action? beforeUnixMetadataRecheck,
        Action? beforeWindowsNoReplaceMove,
        Action? afterUnixExchange)
        => CommitTemporaryFile(
            temporaryPath,
            destinationPath,
            overwrite,
            cancellationToken,
            beforeUnixMetadataRecheck,
            beforeWindowsNoReplaceMove,
            afterUnixExchange,
            beforeUnixCommit: null);

    internal static void CommitTemporaryFile(
        string temporaryPath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken,
        Action? beforeUnixMetadataRecheck,
        Action? beforeWindowsNoReplaceMove,
        Action? afterUnixExchange,
        Action? beforeUnixCommit)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!overwrite)
        {
            try
            {
                File.Move(temporaryPath, destinationPath);
            }
            catch (IOException exception) when (File.Exists(destinationPath))
            {
                throw new IOException(
                    "The destination file already exists. Use -Force to overwrite it.",
                    exception);
            }
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new InvalidOperationException(
                "Windows atomic commits require the still-open secure staging stream so its kernel identity remains pinned.");
        }

        CommitUnixOverwrite(
            temporaryPath,
            destinationPath,
            cancellationToken,
            beforeUnixMetadataRecheck,
            afterUnixExchange,
            beforeUnixCommit);
    }

    /// <summary>Commits the exact secure staging file represented by the still-open stream.</summary>
    internal static void CommitTemporaryFile(
        FileStream temporaryStream,
        string temporaryPath,
        string destinationPath,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        if (temporaryStream is null) throw new ArgumentNullException(nameof(temporaryStream));
        cancellationToken.ThrowIfCancellationRequested();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            CommitWindowsPinnedHandle(temporaryStream, destinationPath, overwrite, cancellationToken);
            return;
        }

        temporaryStream.Flush(flushToDisk: true);
        cancellationToken.ThrowIfCancellationRequested();
        CommitUnixPinnedFile(
            temporaryStream.SafeFileHandle,
            temporaryPath,
            destinationPath,
            overwrite,
            cancellationToken);
    }

    internal static FileStream CreateSecureTemporaryFileStream(string temporaryPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CreateSecureUnixTemporaryFileStream(temporaryPath);
        }

        return CreateSecureWindowsTemporaryFileStream(temporaryPath);
    }

    internal static async Task WriteAllBytesAsync(
        Stream stream,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (bytes is null) throw new ArgumentNullException(nameof(bytes));
        cancellationToken.ThrowIfCancellationRequested();
        for (var offset = 0; offset < bytes.Length; offset += ExportWriteChunkSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(ExportWriteChunkSize, bytes.Length - offset);
            await stream.WriteAsync(bytes, offset, count, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void PreserveDestinationPermissions(string destinationPath, string temporaryPath)
        => PreserveDestinationPermissions(destinationPath, temporaryPath, useManagedApis: true);

    internal static void PreserveDestinationPermissions(
        string destinationPath,
        string temporaryPath,
        bool useManagedApis)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;
        PreserveUnixDestinationMetadata(destinationPath, temporaryPath, useManagedApis);
    }

    private static int UnixModeOffset()
    {
        var operatingSystem = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? "OSX"
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "Linux" : string.Empty;
        return UnixModeOffset(operatingSystem, RuntimeInformation.ProcessArchitecture.ToString());
    }

    internal static int UnixModeOffset(string operatingSystem, string architecture)
    {
        if (string.Equals(operatingSystem, "OSX", StringComparison.Ordinal)) return 4;
        if (!string.Equals(operatingSystem, "Linux", StringComparison.Ordinal))
            throw new PlatformNotSupportedException("Unix file-mode preservation supports Linux and macOS.");

        return architecture switch
        {
            "X64" or "S390x" or "Ppc64le" => 24,
            "X86" or "Arm" or "Arm64" or "Armv6" or "LoongArch64" or "RiscV64" => 16,
            _ => throw new PlatformNotSupportedException(
                "Unix file-mode preservation does not recognize the current Linux architecture.")
        };
    }

}
