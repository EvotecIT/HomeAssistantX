using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Turns switches on, off, or toggles them through a resolved Home Assistant target.</summary>
/// <example>
///   <summary>Turn on switches in an area</summary>
///   <code>Set-HomeAssistantSwitch -Area Utility -Power On -WhatIf</code>
/// </example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantSwitch", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantSwitchCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Turns the selected switches on, off, or toggles their current power.</summary>
    [Parameter(Mandatory = true)]
    public HomeAssistantPowerAction Power { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantPowerAction), Power))
        {
            throw new ArgumentOutOfRangeException(nameof(Power), Power, "Unsupported switch power action.");
        }

        var target = await ResolveTargetAsync("switch").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, "Set switch power to " + Power))
        {
            return;
        }

        var result = Power switch
        {
            HomeAssistantPowerAction.On => await Client.Controls.Switches.TurnOnAsync(target.Target, CancelToken).ConfigureAwait(false),
            HomeAssistantPowerAction.Off => await Client.Controls.Switches.TurnOffAsync(target.Target, CancelToken).ConfigureAwait(false),
            HomeAssistantPowerAction.Toggle => await Client.Controls.Switches.ToggleAsync(target.Target, CancelToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(Power), Power, "Unsupported switch power action.")
        };
        WriteObject(result);
    }
}
