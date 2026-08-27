using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;
namespace HomeAssistantX.PowerShell;

/// <summary>Sets humidifier power, humidity, or mode.</summary>
/// <example><summary>Set bedroom target humidity</summary><code>Set-HomeAssistantHumidifier -Entity humidifier.bedroom -HumidityPercent 50</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantHumidifier", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantHumidifierCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Turns the humidifier on, off, or toggles it.</summary>
    [Parameter] public HomeAssistantHumidifierAction? Action { get; set; }
    /// <summary>Target humidity from 0 through 100 percent.</summary>
    [Parameter][ValidateRange(0d, 100d)] public double? HumidityPercent { get; set; }
    /// <summary>Mode supported by the target humidifier.</summary>
    [Parameter][ValidateNotNullOrEmpty] public string? Mode { get; set; }
    protected override async Task ProcessTargetRecordAsync()
    {
        var mode = Mode is null
            ? null
            : string.IsNullOrWhiteSpace(Mode)
                ? throw new ArgumentException("Mode must not be blank.", nameof(Mode))
                : Mode.Trim();
        var count = (Action.HasValue ? 1 : 0) + (HumidityPercent.HasValue ? 1 : 0) + (mode is not null ? 1 : 0);
        if (count != 1) throw new ArgumentException("Specify exactly one humidifier operation.");
        if (Action.HasValue && !Enum.IsDefined(typeof(HomeAssistantHumidifierAction), Action.Value)) throw new ArgumentOutOfRangeException(nameof(Action));
        var target = await ResolveTargetAsync("humidifier").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, "Set humidifier")) return;
        WriteObject(Action.HasValue ? await Client.Controls.Humidifiers.ActAsync(target.Target, Action.Value, CancelToken).ConfigureAwait(false)
            : HumidityPercent.HasValue ? await Client.Controls.Humidifiers.SetHumidityAsync(target.Target, HumidityPercent.Value, CancelToken).ConfigureAwait(false)
            : await Client.Controls.Humidifiers.SetModeAsync(target.Target, mode!, CancelToken).ConfigureAwait(false));
    }
}
