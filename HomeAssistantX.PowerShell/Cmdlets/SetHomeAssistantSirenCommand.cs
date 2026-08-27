using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;
namespace HomeAssistantX.PowerShell;

/// <summary>Controls sirens with typed tone, volume, and duration options.</summary>
/// <example><summary>Preview a ten-second siren alert</summary><code>Set-HomeAssistantSiren -Entity siren.house -Action TurnOn -Tone alarm -VolumePercent 40 -Duration '00:00:10' -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantSiren", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantSirenCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Turns the siren on, off, or toggles it.</summary>
    [Parameter(Mandatory = true)] public HomeAssistantSirenAction Action { get; set; }
    /// <summary>Named tone supported by the target siren.</summary>
    [Parameter][ValidateNotNullOrEmpty] public string? Tone { get; set; }
    /// <summary>Numeric tone identifier supported by the target siren.</summary>
    [Parameter] public int? ToneId { get; set; }
    /// <summary>Volume from 0 through 100 percent.</summary>
    [Parameter][ValidateRange(0d, 100d)] public double? VolumePercent { get; set; }
    /// <summary>Positive whole-second duration for TurnOn.</summary>
    [Parameter] public TimeSpan? Duration { get; set; }
    protected override async Task ProcessTargetRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantSirenAction), Action)) throw new ArgumentOutOfRangeException(nameof(Action));
        if (Tone is not null && ToneId.HasValue) throw new ArgumentException("Tone and ToneId cannot be combined.");
        var hasOptions = Tone is not null || ToneId.HasValue || VolumePercent.HasValue || Duration.HasValue;
        if (hasOptions && Action != HomeAssistantSirenAction.TurnOn) throw new ArgumentException("Tone, volume, and duration apply only to TurnOn.");
        if (Duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(Duration));
        var options = hasOptions ? new HomeAssistantSirenOptions { Tone = Tone, ToneId = ToneId, VolumePercent = VolumePercent, Duration = Duration } : null;
        var target = await ResolveTargetAsync("siren").ConfigureAwait(false);
        if (ShouldProcess(target.Description, Action.ToString())) WriteObject(await Client.Controls.Sirens.ActAsync(target.Target, Action, options, CancelToken).ConfigureAwait(false));
    }
}
