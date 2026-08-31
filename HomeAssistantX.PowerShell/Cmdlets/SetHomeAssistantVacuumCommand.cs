using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;
namespace HomeAssistantX.PowerShell;

/// <summary>Controls common vacuum lifecycle, fan-speed, and area-cleaning operations.</summary>
/// <example><summary>Return a vacuum to its base</summary><code>Set-HomeAssistantVacuum -Entity vacuum.downstairs -Action ReturnToBase</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantVacuum", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantVacuumCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Starts, pauses, stops, docks, locates, or spot-cleans the vacuum.</summary>
    [Parameter] public HomeAssistantVacuumAction? Action { get; set; }
    /// <summary>Fan-speed name supported by the target vacuum.</summary>
    [Parameter][ValidateNotNullOrEmpty] public string? FanSpeed { get; set; }
    /// <summary>One or more provider area identifiers for an area-cleaning operation.</summary>
    [Parameter][ValidateNotNullOrEmpty] public string[]? CleaningAreaId { get; set; }
    protected override async Task ProcessTargetRecordAsync()
    {
        var fanSpeed = FanSpeed is null
            ? null
            : ControlValidation.RequiredUnchanged(FanSpeed, nameof(FanSpeed), CancelToken);
        var cleaningAreaIds = CleaningAreaId is null
            ? null
            : ControlValidation.RequiredValuesUnchanged(CleaningAreaId, nameof(CleaningAreaId), CancelToken);
        var count = (Action.HasValue ? 1 : 0) + (fanSpeed is not null ? 1 : 0) + (cleaningAreaIds is { Count: > 0 } ? 1 : 0);
        if (count != 1) throw new ArgumentException("Specify exactly one vacuum operation.");
        if (Action.HasValue && !Enum.IsDefined(typeof(HomeAssistantVacuumAction), Action.Value)) throw new ArgumentOutOfRangeException(nameof(Action));
        var target = await ResolveTargetAsync("vacuum").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, "Set vacuum")) return;
        WriteObject(Action.HasValue ? await Client.Controls.Vacuums.ActAsync(target.Target, Action.Value, CancelToken).ConfigureAwait(false)
            : fanSpeed is not null ? await Client.Controls.Vacuums.SetFanSpeedAsync(target.Target, fanSpeed, CancelToken).ConfigureAwait(false)
            : await Client.Controls.Vacuums.CleanAreaAsync(target.Target, cleaningAreaIds!, CancelToken).ConfigureAwait(false));
    }
}
