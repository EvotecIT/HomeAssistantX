using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Sets one fan action, speed, oscillation, direction, or preset.</summary>
/// <example><summary>Set an office fan to 35 percent</summary><code>Set-HomeAssistantFan -Entity fan.office -Percentage 35 -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantFan", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantFanCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Power or relative speed action.</summary>
    [Parameter] public HomeAssistantFanAction? Action { get; set; }
    /// <summary>Absolute fan speed from 0 through 100 percent.</summary>
    [Parameter][ValidateRange(0, 100)] public int? Percentage { get; set; }
    /// <summary>Enables or disables oscillation.</summary>
    [Parameter] public bool? Oscillating { get; set; }
    /// <summary>Sets forward or reverse direction.</summary>
    [Parameter] public HomeAssistantFanDirection? Direction { get; set; }
    /// <summary>Selects a preset mode supported by the target.</summary>
    [Parameter][ValidateNotNullOrEmpty] public string? PresetMode { get; set; }
    /// <summary>Optional percentage step for IncreaseSpeed or DecreaseSpeed.</summary>
    [Parameter][ValidateRange(0, 100)] public int? PercentageStep { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        var presetMode = PresetMode is null
            ? null
            : string.IsNullOrWhiteSpace(PresetMode)
                ? throw new ArgumentException("PresetMode must not be blank.", nameof(PresetMode))
                : PresetMode.Trim();
        var count = (Action.HasValue ? 1 : 0) + (Percentage.HasValue ? 1 : 0) + (Oscillating.HasValue ? 1 : 0) + (Direction.HasValue ? 1 : 0) + (presetMode is not null ? 1 : 0);
        if (count != 1) throw new ArgumentException("Specify exactly one fan operation.");
        if (Action.HasValue && !Enum.IsDefined(typeof(HomeAssistantFanAction), Action.Value)) throw new ArgumentOutOfRangeException(nameof(Action));
        if (Direction.HasValue && !Enum.IsDefined(typeof(HomeAssistantFanDirection), Direction.Value)) throw new ArgumentOutOfRangeException(nameof(Direction));
        if (PercentageStep.HasValue && Action is not HomeAssistantFanAction.IncreaseSpeed and not HomeAssistantFanAction.DecreaseSpeed) throw new ArgumentException("PercentageStep requires IncreaseSpeed or DecreaseSpeed.", nameof(PercentageStep));
        var target = await ResolveTargetAsync("fan").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, "Set fan")) return;
        var result = Action.HasValue ? await Client.Controls.Fans.ActAsync(target.Target, Action.Value, PercentageStep, CancelToken).ConfigureAwait(false)
            : Percentage.HasValue ? await Client.Controls.Fans.SetPercentageAsync(target.Target, Percentage.Value, CancelToken).ConfigureAwait(false)
            : Oscillating.HasValue ? await Client.Controls.Fans.SetOscillationAsync(target.Target, Oscillating.Value, CancelToken).ConfigureAwait(false)
            : Direction.HasValue ? await Client.Controls.Fans.SetDirectionAsync(target.Target, Direction.Value, CancelToken).ConfigureAwait(false)
            : await Client.Controls.Fans.SetPresetModeAsync(target.Target, presetMode!, CancelToken).ConfigureAwait(false);
        WriteObject(result);
    }
}
