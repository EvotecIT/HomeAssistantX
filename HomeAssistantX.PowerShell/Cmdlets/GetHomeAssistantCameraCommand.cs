using System.Management.Automation;
using HomeAssistantX.Cameras;

namespace HomeAssistantX.PowerShell;

/// <summary>Reads camera state, capabilities, stream details, preferences, or temporary signed paths.</summary>
/// <example><summary>List camera state</summary><code>Get-HomeAssistantCamera</code></example>
/// <example><summary>Request a playable HLS path</summary><code>Get-HomeAssistantCamera camera.front -Stream</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantCamera", DefaultParameterSetName = StatusSet)]
[OutputType(typeof(HomeAssistantCameraStatus))]
[OutputType(typeof(HomeAssistantCameraCapabilities))]
[OutputType(typeof(HomeAssistantCameraStream))]
[OutputType(typeof(HomeAssistantCameraPreferences))]
[OutputType(typeof(string))]
public sealed class GetHomeAssistantCameraCommand : HomeAssistantCmdlet
{
    private const string StatusSet = "Status";
    private const string CapabilitiesSet = "Capabilities";
    private const string StreamSet = "Stream";
    private const string PreferencesSet = "Preferences";
    private const string SignedImageSet = "SignedImage";
    private const string SignedMjpegSet = "SignedMjpeg";

    /// <summary>Identifies one camera. Optional when listing camera status; required for capabilities, streams, preferences, and signed paths.</summary>
    [Parameter(Position = 0, ParameterSetName = StatusSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = CapabilitiesSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = StreamSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = PreferencesSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = SignedImageSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = SignedMjpegSet)]
    [ValidateNotNullOrEmpty]
    public string? EntityId { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = CapabilitiesSet)][ValidateSwitchPresent] public SwitchParameter Capabilities { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = StreamSet)][ValidateSwitchPresent] public SwitchParameter Stream { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = PreferencesSet)][ValidateSwitchPresent] public SwitchParameter Preferences { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = SignedImageSet)][ValidateSwitchPresent] public SwitchParameter SignedImage { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = SignedMjpegSet)][ValidateSwitchPresent] public SwitchParameter SignedMjpeg { get; set; }
    [Parameter(ParameterSetName = SignedImageSet)][ValidateRange(1, int.MaxValue)] public int? Width { get; set; }
    [Parameter(ParameterSetName = SignedImageSet)][ValidateRange(1, int.MaxValue)] public int? Height { get; set; }
    [Parameter(ParameterSetName = SignedMjpegSet)][ValidateRange(0.5, double.MaxValue)] public double? IntervalSeconds { get; set; }
    [Parameter(ParameterSetName = SignedImageSet)]
    [Parameter(ParameterSetName = SignedMjpegSet)]
    [ValidateRange(1, int.MaxValue)] public int? ExpiresInSeconds { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        switch (ParameterSetName)
        {
            case CapabilitiesSet:
                WriteObject(await Client.Cameras.GetCapabilitiesAsync(EntityId!, CancelToken).ConfigureAwait(false));
                break;
            case StreamSet:
                WriteObject(await Client.Cameras.GetStreamAsync(EntityId!, CancelToken).ConfigureAwait(false));
                break;
            case PreferencesSet:
                WriteObject(await Client.Cameras.GetPreferencesAsync(EntityId!, CancelToken).ConfigureAwait(false));
                break;
            case SignedImageSet:
                WriteObject(await Client.Cameras.GetSignedImagePathAsync(EntityId!, Expiration(), Width, Height, CancelToken).ConfigureAwait(false));
                break;
            case SignedMjpegSet:
                WriteObject(await Client.Cameras.GetSignedMjpegStreamPathAsync(EntityId!, Expiration(), IntervalSeconds, CancelToken).ConfigureAwait(false));
                break;
            default:
                if (EntityId is null) WriteObject(await Client.Cameras.GetAsync(CancelToken).ConfigureAwait(false), true);
                else WriteObject(await Client.Cameras.GetAsync(EntityId, CancelToken).ConfigureAwait(false));
                break;
        }
    }

    private TimeSpan? Expiration() => ExpiresInSeconds.HasValue ? TimeSpan.FromSeconds(ExpiresInSeconds.Value) : null;
}
