using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Energy;

namespace HomeAssistantX.PowerShell;

/// <summary>Reads Energy preferences, capabilities, validation, provider forecasts, or fossil-energy periods.</summary>
/// <example><summary>Inspect Energy dashboard preferences</summary><code>Get-HomeAssistantEnergy</code></example>
/// <example><summary>Calculate hourly fossil energy</summary><code>Get-HomeAssistantEnergy -FossilConsumption -StartTime (Get-Date).AddDays(-1) -EndTime (Get-Date) -EnergyStatisticId sensor.grid_energy -Co2StatisticId sensor.co2_intensity -Period Hour</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantEnergy", DefaultParameterSetName = PreferencesSet)]
[OutputType(typeof(HomeAssistantEnergyPreferences))]
[OutputType(typeof(HomeAssistantEnergyInfo))]
[OutputType(typeof(JsonElement))]
[OutputType(typeof(IReadOnlyDictionary<string, JsonElement>))]
[OutputType(typeof(HomeAssistantFossilEnergyPeriod))]
public sealed class GetHomeAssistantEnergyCommand : HomeAssistantCmdlet
{
    private const string PreferencesSet = "Preferences";
    private const string InfoSet = "Info";
    private const string ValidationSet = "Validation";
    private const string SolarSet = "SolarForecast";
    private const string FossilSet = "FossilConsumption";

    [Parameter(ParameterSetName = PreferencesSet)] public SwitchParameter Preferences { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = InfoSet)][ValidateSwitchPresent] public SwitchParameter Info { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ValidationSet)][ValidateSwitchPresent] public SwitchParameter Validation { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = SolarSet)][ValidateSwitchPresent] public SwitchParameter SolarForecast { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = FossilSet)][ValidateSwitchPresent] public SwitchParameter FossilConsumption { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = FossilSet)] public DateTimeOffset StartTime { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = FossilSet)] public DateTimeOffset EndTime { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = FossilSet)][ValidateNotNullOrEmpty] public string[] EnergyStatisticId { get; set; } = Array.Empty<string>();
    [Parameter(Mandatory = true, ParameterSetName = FossilSet)][ValidateNotNullOrEmpty] public string Co2StatisticId { get; set; } = string.Empty;
    [Parameter(Mandatory = true, ParameterSetName = FossilSet)] public HomeAssistantEnergyPeriod Period { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        object result = ParameterSetName switch
        {
            InfoSet => await Client.Energy.GetInfoAsync(CancelToken).ConfigureAwait(false),
            ValidationSet => await Client.Energy.ValidateAsync(CancelToken).ConfigureAwait(false),
            SolarSet => await Client.Energy.GetSolarForecastAsync(CancelToken).ConfigureAwait(false),
            FossilSet => await Client.Energy.GetFossilEnergyConsumptionAsync(StartTime, EndTime, EnergyStatisticId, Co2StatisticId, Period, CancelToken).ConfigureAwait(false),
            _ => await Client.Energy.GetPreferencesAsync(CancelToken).ConfigureAwait(false)
        };
        WriteObject(result, result is System.Collections.IEnumerable && result is not string && ParameterSetName == FossilSet);
    }
}
