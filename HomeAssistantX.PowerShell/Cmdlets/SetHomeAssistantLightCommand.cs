using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Controls lights with typed power, brightness, color, effect, and transition parameters.</summary>
/// <example>
///   <summary>Preview setting every Kitchen light</summary>
///   <code>Set-HomeAssistantLight -Area Kitchen -Power On -BrightnessPercent 45 -WhatIf</code>
/// </example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantLight", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantLightCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Turns the selected lights on, off, or toggles their current power.</summary>
    [Parameter(Mandatory = true)]
    public HomeAssistantPowerAction Power { get; set; }

    /// <summary>Brightness from 0 through 100 percent.</summary>
    [Parameter]
    [ValidateRange(0d, 100d)]
    public double? BrightnessPercent { get; set; }

    /// <summary>Color temperature in kelvin.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? ColorTemperatureKelvin { get; set; }

    /// <summary>Red, green, and blue values, each from 0 through 255.</summary>
    [Parameter]
    [ValidateCount(3, 3)]
    [ValidateRange(0, 255)]
    public int[]? RgbColor { get; set; }

    /// <summary>Effect name exposed by the selected lights.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Effect { get; set; }

    /// <summary>Transition duration from 0 through 6553 seconds.</summary>
    [Parameter]
    [ValidateRange(0d, 6553d)]
    public double? TransitionSeconds { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantPowerAction), Power))
        {
            throw new ArgumentOutOfRangeException(nameof(Power), Power, "Unsupported light power action.");
        }

        var transition = TransitionSeconds.HasValue ? TimeSpan.FromSeconds(TransitionSeconds.Value) : (TimeSpan?)null;
        var effect = Effect is null ? null : ControlValidation.RequiredUnchanged(Effect, nameof(Effect), CancelToken);
        var options = CreateOptions(transition, effect);
        var target = await ResolveTargetAsync("light").ConfigureAwait(false);
        if (Power == HomeAssistantPowerAction.Off
            && (BrightnessPercent.HasValue || ColorTemperatureKelvin.HasValue || RgbColor is not null || effect is not null))
        {
            throw new ArgumentException("Brightness, color, and effect parameters require -Power On or Toggle.");
        }

        if (!ShouldProcess(target.Description, "Set light power to " + Power))
        {
            return;
        }

        HomeAssistantServiceCallResult result;
        switch (Power)
        {
            case HomeAssistantPowerAction.On:
                result = await Client.Controls.Lights.TurnOnAsync(target.Target, options, CancelToken).ConfigureAwait(false);
                break;
            case HomeAssistantPowerAction.Off:
                result = await Client.Controls.Lights.TurnOffAsync(target.Target, transition, CancelToken).ConfigureAwait(false);
                break;
            case HomeAssistantPowerAction.Toggle:
                result = await Client.Controls.Lights.ToggleAsync(target.Target, options, CancelToken).ConfigureAwait(false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(Power), Power, "Unsupported light power action.");
        }

        WriteObject(result);
    }

    private HomeAssistantLightOptions CreateOptions(TimeSpan? transition, string? effect)
    {
        var options = new HomeAssistantLightOptions
        {
            BrightnessPercent = BrightnessPercent,
            ColorTemperatureKelvin = ColorTemperatureKelvin,
            RgbColor = RgbColor,
            Transition = transition
        };
        options.SetValidatedEffect(effect);
        return options;
    }
}
