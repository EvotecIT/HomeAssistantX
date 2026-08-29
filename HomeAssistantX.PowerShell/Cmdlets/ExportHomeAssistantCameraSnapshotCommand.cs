using System.Management.Automation;
using HomeAssistantX.IO;
using HomeAssistantX.Models;

namespace HomeAssistantX.PowerShell;

/// <summary>Exports one bounded camera snapshot through an atomic local-file replacement.</summary>
/// <example><summary>Save a scaled snapshot</summary><code>Export-HomeAssistantCameraSnapshot camera.front ./front.jpg -Width 1280 -Height 720</code></example>
[Cmdlet(VerbsData.Export, "HomeAssistantCameraSnapshot", SupportsShouldProcess = true)]
[OutputType(typeof(FileInfo))]
public sealed class ExportHomeAssistantCameraSnapshotCommand : HomeAssistantCmdlet
{
    private string _entityId = string.Empty;
    private string _resolvedPath = string.Empty;
    [Parameter(Mandatory = true, Position = 0)][ValidateNotNullOrEmpty] public string EntityId { get; set; } = string.Empty;
    [Parameter(Mandatory = true, Position = 1)][ValidateNotNullOrEmpty] public string Path { get; set; } = string.Empty;
    [Parameter][ValidateRange(1, int.MaxValue)] public int? Width { get; set; }
    [Parameter][ValidateRange(1, int.MaxValue)] public int? Height { get; set; }
    [Parameter] public SwitchParameter Force { get; set; }

    protected override Task BeginProcessingAsync()
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(EntityId, "camera", CancelToken, out _entityId))
        {
            throw new ArgumentException("A lowercase camera entity identifier is required.", nameof(EntityId));
        }

        _resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        if (Width.HasValue != Height.HasValue) throw new ArgumentException("Width and Height must be supplied together.");
        return Task.CompletedTask;
    }

    protected override async Task ProcessRecordAsync()
    {
        if (File.Exists(_resolvedPath) && !Force) throw new IOException("The destination file already exists. Use -Force to overwrite it.");
        var directory = System.IO.Path.GetDirectoryName(_resolvedPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) throw new DirectoryNotFoundException("The snapshot destination directory does not exist.");
        if (!ShouldProcess(_resolvedPath, "Export Home Assistant camera snapshot")) return;
        var bytes = await Client.Cameras.GetSnapshotAsync(_entityId, Width, Height, CancelToken).ConfigureAwait(false);
        var temporaryPath = System.IO.Path.Combine(directory, "." + System.IO.Path.GetFileName(_resolvedPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length, CancelToken).ConfigureAwait(false);
                await stream.FlushAsync(CancelToken).ConfigureAwait(false);
            }
            HomeAssistantAtomicFile.CommitTemporaryFile(temporaryPath, _resolvedPath, Force, CancelToken);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
        WriteObject(new FileInfo(_resolvedPath));
    }
}
