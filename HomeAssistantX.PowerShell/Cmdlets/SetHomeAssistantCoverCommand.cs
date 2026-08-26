using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Moves covers with a typed action, position, or tilt position.</summary>
/// <example>
///   <summary>Set a cover to 60 percent</summary>
///   <code>Set-HomeAssistantCover -Entity cover.kitchen -PositionPercent 60 -WhatIf</code>
/// </example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantCover", SupportsShouldProcess = true, DefaultParameterSetName = ActionParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantCoverCommand : HomeAssistantTargetCmdlet
{
    private const string ActionParameterSet = EntityParameterSet;

    /// <summary>Opens, closes, stops, or toggles the selected covers.</summary>
    [Parameter]
    public HomeAssistantCoverAction? Action { get; set; }

    /// <summary>Cover position from 0 through 100 percent.</summary>
    [Parameter]
    [ValidateRange(0d, 100d)]
    public double? PositionPercent { get; set; }

    /// <summary>Cover tilt position from 0 through 100 percent.</summary>
    [Parameter]
    [ValidateRange(0d, 100d)]
    public double? TiltPositionPercent { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var operationCount = (Action.HasValue ? 1 : 0) + (PositionPercent.HasValue ? 1 : 0) + (TiltPositionPercent.HasValue ? 1 : 0);
        if (operationCount != 1)
        {
            throw new ArgumentException("Specify exactly one of -Action, -PositionPercent, or -TiltPositionPercent.");
        }

        var target = await ResolveTargetAsync("cover").ConfigureAwait(false);
        var operation = Action.HasValue ? Action.Value.ToString() : PositionPercent.HasValue ? "Set position" : "Set tilt position";
        if (!ShouldProcess(target.Description, operation))
        {
            return;
        }

        var result = Action.HasValue
            ? await Client.Controls.Covers.ActAsync(target.Target, Action.Value, CancelToken).ConfigureAwait(false)
            : PositionPercent.HasValue
                ? await Client.Controls.Covers.SetPositionAsync(target.Target, PositionPercent.Value, CancelToken).ConfigureAwait(false)
                : await Client.Controls.Covers.SetTiltPositionAsync(target.Target, TiltPositionPercent!.Value, CancelToken).ConfigureAwait(false);
        WriteObject(result);
    }
}
