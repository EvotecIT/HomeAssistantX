using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Sets common climate values with typed parameters instead of raw action data.</summary>
/// <example>
///   <summary>Set a thermostat temperature and HVAC mode</summary>
///   <code>Set-HomeAssistantClimate -Entity climate.downstairs -Temperature 21.5 -HvacMode heat -WhatIf</code>
/// </example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantClimate", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantClimateCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Target temperature in the entity's configured unit.</summary>
    [Parameter]
    public double? Temperature { get; set; }

    /// <summary>Lower target temperature for range-based HVAC modes.</summary>
    [Parameter]
    public double? TargetTemperatureLow { get; set; }

    /// <summary>Upper target temperature for range-based HVAC modes.</summary>
    [Parameter]
    public double? TargetTemperatureHigh { get; set; }

    /// <summary>HVAC mode supported by the target, such as <c>heat</c>, <c>cool</c>, or <c>auto</c>.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? HvacMode { get; set; }

    /// <summary>Fan mode supported by the target.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? FanMode { get; set; }

    /// <summary>Preset mode supported by the target.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? PresetMode { get; set; }

    /// <summary>Target humidity from 0 through 100 percent.</summary>
    [Parameter]
    [ValidateRange(0d, 100d)]
    public double? Humidity { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        ValidateFinite(Temperature, nameof(Temperature));
        ValidateFinite(TargetTemperatureLow, nameof(TargetTemperatureLow));
        ValidateFinite(TargetTemperatureHigh, nameof(TargetTemperatureHigh));
        HvacMode = NormalizeOptionalMode(HvacMode, nameof(HvacMode));
        FanMode = NormalizeOptionalMode(FanMode, nameof(FanMode));
        PresetMode = NormalizeOptionalMode(PresetMode, nameof(PresetMode));

        if (!Temperature.HasValue
            && !TargetTemperatureLow.HasValue
            && !TargetTemperatureHigh.HasValue
            && string.IsNullOrWhiteSpace(HvacMode)
            && string.IsNullOrWhiteSpace(FanMode)
            && string.IsNullOrWhiteSpace(PresetMode)
            && !Humidity.HasValue)
        {
            throw new ArgumentException("Specify at least one climate value.");
        }

        if (TargetTemperatureLow.HasValue != TargetTemperatureHigh.HasValue)
        {
            throw new ArgumentException("TargetTemperatureLow and TargetTemperatureHigh must be supplied together.");
        }

        if (Temperature.HasValue && TargetTemperatureLow.HasValue)
        {
            throw new ArgumentException("Temperature cannot be combined with a target temperature range.");
        }

        if (TargetTemperatureLow.HasValue && TargetTemperatureLow.Value > TargetTemperatureHigh!.Value)
        {
            throw new ArgumentException("TargetTemperatureLow cannot be greater than TargetTemperatureHigh.");
        }

        var options = new HomeAssistantClimateOptions
        {
            Temperature = Temperature,
            TargetTemperatureLow = TargetTemperatureLow,
            TargetTemperatureHigh = TargetTemperatureHigh,
            HvacMode = HvacMode,
            FanMode = FanMode,
            PresetMode = PresetMode,
            Humidity = Humidity
        };
        var target = await ResolveTargetAsync("climate").ConfigureAwait(false);
        if (ShouldProcess(target.Description, "Set climate values"))
        {
            WriteObject(await Client.Controls.Climate.SetAsync(target.Target, options, CancelToken).ConfigureAwait(false), true);
        }
    }

    private static void ValidateFinite(double? value, string name)
    {
        if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
        {
            throw new ArgumentOutOfRangeException(name, "The value must be a finite number.");
        }
    }

    private static string? NormalizeOptionalMode(string? value, string name)
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty mode is required.", name);
        return value.Trim();
    }
}
