using System.Management.Automation;
using HomeAssistantX.IO;

namespace HomeAssistantX.PowerShell;

/// <summary>Downloads Home Assistant-redacted diagnostics for a configuration entry or one device.</summary>
/// <example>
///   <summary>Export a configuration-entry diagnostic</summary>
///   <code>$ha | Export-HomeAssistantDiagnostic -EntryId 'entry-id' -Path './diagnostic.json'</code>
///   <para>Writes the diagnostic atomically after Home Assistant applies its redaction.</para>
/// </example>
[Cmdlet(VerbsData.Export, "HomeAssistantDiagnostic", SupportsShouldProcess = true, DefaultParameterSetName = ConfigEntryParameterSet)]
[OutputType(typeof(FileInfo))]
public sealed class ExportHomeAssistantDiagnosticCommand : HomeAssistantCmdlet
{
    private const string ConfigEntryParameterSet = "ConfigEntry";
    private const string DeviceParameterSet = "Device";
    private string _resolvedPath = string.Empty;

    /// <summary>Configuration-entry identifier for the diagnostic export.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string EntryId { get; set; } = string.Empty;

    /// <summary>Optional device identifier within the selected configuration entry.</summary>
    [Parameter(Mandatory = true, ParameterSetName = DeviceParameterSet)]
    [ValidateNotNullOrEmpty]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Destination file path. Diagnostic bundles can contain sensitive installation data.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Path { get; set; } = string.Empty;

    /// <summary>Overwrite an existing destination file.</summary>
    [Parameter]
    public SwitchParameter Force { get; set; }

    protected override Task BeginProcessingAsync()
    {
        _resolvedPath = SessionState.Path.GetUnresolvedProviderPathFromPSPath(Path);
        return Task.CompletedTask;
    }

    protected override async Task ProcessRecordAsync()
    {
        if (File.Exists(_resolvedPath) && !Force)
        {
            throw new IOException("The destination file already exists. Use -Force to overwrite it.");
        }

        if (!ShouldProcess(_resolvedPath, "Export Home Assistant diagnostic"))
        {
            return;
        }

        var bytes = ParameterSetName == DeviceParameterSet
            ? await Client.Operations.Diagnostics.GetDeviceAsync(EntryId, DeviceId, CancelToken).ConfigureAwait(false)
            : await Client.Operations.Diagnostics.GetConfigEntryAsync(EntryId, CancelToken).ConfigureAwait(false);

        var directory = System.IO.Path.GetDirectoryName(_resolvedPath)
            ?? throw new IOException("The diagnostic destination directory could not be resolved.");
        var temporaryPath = System.IO.Path.Combine(
            directory,
            "." + System.IO.Path.GetFileName(_resolvedPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                81920,
                useAsync: true))
            {
                await stream.WriteAsync(bytes, 0, bytes.Length, CancelToken).ConfigureAwait(false);
                await stream.FlushAsync(CancelToken).ConfigureAwait(false);
            }

            if (File.Exists(_resolvedPath))
            {
                if (!Force)
                {
                    throw new IOException("The destination file already exists. Use -Force to overwrite it.");
                }

                HomeAssistantAtomicFile.PreserveDestinationPermissions(_resolvedPath, temporaryPath);
                File.Replace(temporaryPath, _resolvedPath, null);
            }
            else
            {
                File.Move(temporaryPath, _resolvedPath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }

        WriteObject(new FileInfo(_resolvedPath));
    }
}
