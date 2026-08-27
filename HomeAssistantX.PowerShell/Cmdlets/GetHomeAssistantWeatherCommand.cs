using System.Management.Automation;
using HomeAssistantX.Weather;

namespace HomeAssistantX.PowerShell;

/// <summary>Reads current observations, forecasts, or supported weather units.</summary>
/// <example><summary>List weather entities</summary><code>Get-HomeAssistantWeather</code></example>
/// <example><summary>Read the daily forecast</summary><code>Get-HomeAssistantWeather -EntityId weather.home -Forecast -ForecastType Daily</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantWeather", DefaultParameterSetName = CurrentSet)]
[OutputType(typeof(HomeAssistantWeatherObservation))]
[OutputType(typeof(HomeAssistantWeatherForecastUpdate))]
[OutputType(typeof(IReadOnlyDictionary<string, IReadOnlyList<string>>))]
public sealed class GetHomeAssistantWeatherCommand : HomeAssistantCmdlet
{
    private const string CurrentSet = "Current";
    private const string ForecastSet = "Forecast";
    private const string UnitsSet = "Units";
    [Parameter(Position = 0, ParameterSetName = CurrentSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ForecastSet)]
    [ValidateNotNullOrEmpty]
    public string? EntityId { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ForecastSet)][ValidateSwitchPresent] public SwitchParameter Forecast { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ForecastSet)] public HomeAssistantWeatherForecastType ForecastType { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = UnitsSet)][ValidateSwitchPresent] public SwitchParameter ConvertibleUnits { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == UnitsSet)
        {
            WriteObject(await Client.Weather.GetConvertibleUnitsAsync(CancelToken).ConfigureAwait(false));
            return;
        }
        if (ParameterSetName == ForecastSet)
        {
            WriteObject(await Client.Weather.GetForecastAsync(EntityId!, ForecastType, CancelToken).ConfigureAwait(false));
            return;
        }
        if (EntityId is not null)
            WriteObject(await Client.Weather.GetAsync(EntityId, CancelToken).ConfigureAwait(false));
        else
            WriteObject(await Client.Weather.GetAsync(CancelToken).ConfigureAwait(false), true);
    }
}
