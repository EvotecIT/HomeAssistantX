using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;
namespace HomeAssistantX.PowerShell;

/// <summary>Moves a valve with one typed action or target position.</summary>
/// <example><summary>Preview opening a garden valve halfway</summary><code>Set-HomeAssistantValve -Entity valve.garden -PositionPercent 50 -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantValve", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantValveCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Opens, closes, stops, or toggles the selected valves.</summary>
    [Parameter] public HomeAssistantValveAction? Action { get; set; }
    /// <summary>Target valve position from 0 through 100 percent.</summary>
    [Parameter][ValidateRange(0d, 100d)] public double? PositionPercent { get; set; }
    protected override async Task ProcessTargetRecordAsync()
    {
        if (Action.HasValue == PositionPercent.HasValue) throw new ArgumentException("Specify exactly one of Action or PositionPercent.");
        if (Action.HasValue && !Enum.IsDefined(typeof(HomeAssistantValveAction), Action.Value)) throw new ArgumentOutOfRangeException(nameof(Action));
        var target = await ResolveTargetAsync("valve").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, "Set valve")) return;
        WriteObject(Action.HasValue
            ? await Client.Controls.Valves.ActAsync(target.Target, Action.Value, CancelToken).ConfigureAwait(false)
            : await Client.Controls.Valves.SetPositionAsync(target.Target, PositionPercent!.Value, CancelToken).ConfigureAwait(false));
    }
}
