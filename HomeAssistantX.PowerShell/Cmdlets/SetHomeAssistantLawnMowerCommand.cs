using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;
namespace HomeAssistantX.PowerShell;

/// <summary>Starts, pauses, or docks Home Assistant lawn mower entities.</summary>
/// <example><summary>Preview docking a garden mower</summary><code>Set-HomeAssistantLawnMower -Entity lawn_mower.garden -Action Dock -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantLawnMower", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantLawnMowerCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Starts mowing, pauses, or returns the mower to its dock.</summary>
    [Parameter(Mandatory = true)] public HomeAssistantLawnMowerAction Action { get; set; }
    protected override async Task ProcessTargetRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantLawnMowerAction), Action)) throw new ArgumentOutOfRangeException(nameof(Action));
        var target = await ResolveTargetAsync("lawn_mower").ConfigureAwait(false);
        if (ShouldProcess(target.Description, Action.ToString())) WriteObject(await Client.Controls.LawnMowers.ActAsync(target.Target, Action, CancelToken).ConfigureAwait(false));
    }
}
