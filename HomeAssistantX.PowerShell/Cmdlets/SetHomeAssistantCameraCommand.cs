using System.Management.Automation;
using HomeAssistantX.Cameras;
using HomeAssistantX.Models;

namespace HomeAssistantX.PowerShell;

/// <summary>Updates administrator-only camera streaming preferences.</summary>
/// <example><summary>Preview enabling stream preloading</summary><code>Set-HomeAssistantCamera camera.front -PreloadStream $true -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantCamera", SupportsShouldProcess = true)]
[OutputType(typeof(HomeAssistantCameraPreferences))]
public sealed class SetHomeAssistantCameraCommand : HomeAssistantCmdlet
{
    [Parameter(Mandatory = true, Position = 0)][ValidateNotNullOrEmpty] public string EntityId { get; set; } = string.Empty;
    [Parameter] public bool? PreloadStream { get; set; }
    [Parameter] public HomeAssistantCameraOrientation? Orientation { get; set; }
    [Parameter] public SwitchParameter PassThru { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (!HomeAssistantEntityId.TryNormalize(EntityId, out var entityId)
            || !entityId.StartsWith("camera.", StringComparison.Ordinal))
        {
            throw new ArgumentException("A camera entity identifier is required.", nameof(EntityId));
        }

        if (Orientation.HasValue && !Enum.IsDefined(typeof(HomeAssistantCameraOrientation), Orientation.Value)) throw new ArgumentOutOfRangeException(nameof(Orientation));
        var update = new HomeAssistantCameraPreferencesUpdate { PreloadStream = PreloadStream, Orientation = Orientation };
        if (!PreloadStream.HasValue && !Orientation.HasValue) throw new ArgumentException("Specify PreloadStream or Orientation.");
        if (!ShouldProcess(entityId, "Update Home Assistant camera preferences")) return;
        var result = await Client.Cameras.SavePreferencesAsync(entityId, update, CancelToken).ConfigureAwait(false);
        if (PassThru) WriteObject(result);
    }
}
