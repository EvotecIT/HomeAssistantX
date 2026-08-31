using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;
namespace HomeAssistantX.PowerShell;

/// <summary>Arms, disarms, or triggers alarm panels with high-impact confirmation.</summary>
/// <example><summary>Preview arming the home for the night</summary><code>Set-HomeAssistantAlarm -Entity alarm_control_panel.home -Action ArmNight -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantAlarm", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantAlarmCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Arm, disarm, or trigger action.</summary>
    [Parameter(Mandatory = true)] public HomeAssistantAlarmAction Action { get; set; }
    /// <summary>Optional alarm code sent only to Home Assistant.</summary>
    [Parameter] public string? Code { get; set; }
    protected override async Task ProcessTargetRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantAlarmAction), Action)) throw new ArgumentOutOfRangeException(nameof(Action));
        var code = Code is null ? null : ControlValidation.RequiredUnchanged(Code, nameof(Code), CancelToken);
        var target = await ResolveTargetAsync("alarm_control_panel").ConfigureAwait(false);
        if (ShouldProcess(target.Description, Action.ToString())) WriteObject(await Client.Controls.Alarms.ActAsync(target.Target, Action, code, CancelToken).ConfigureAwait(false));
    }
}
