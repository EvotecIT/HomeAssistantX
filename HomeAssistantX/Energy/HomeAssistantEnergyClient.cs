using System.Globalization;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Energy;

/// <summary>Reads, validates, and configures Home Assistant Energy data.</summary>
public sealed class HomeAssistantEnergyClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantEnergyClient(HomeAssistantWebSocketClient webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task<HomeAssistantEnergyPreferences> GetPreferencesAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("energy/get_prefs", null, cancellationToken).ConfigureAwait(false);
        var preferences = HomeAssistantJson.DeserializeResponse<HomeAssistantEnergyPreferences>(value, "The Home Assistant Energy preferences could not be decoded.");
        ValidatePreferences(preferences);
        return preferences;
    }

    public async Task<HomeAssistantEnergyPreferences> SavePreferencesAsync(
        HomeAssistantEnergyPreferencesUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        var value = await _webSocket.RequestAsync("energy/save_prefs", update.ToPayload(), cancellationToken).ConfigureAwait(false);
        var preferences = HomeAssistantJson.DeserializeResponse<HomeAssistantEnergyPreferences>(value, "The updated Home Assistant Energy preferences could not be decoded.");
        ValidatePreferences(preferences);
        return preferences;
    }

    public async Task<HomeAssistantEnergyInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("energy/info", null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("cost_sensors", out var costSensors)
            || costSensors.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("solar_forecast_domains", out var forecastDomains)
            || forecastDomains.ValueKind != JsonValueKind.Array
            || forecastDomains.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())))
        {
            throw new HomeAssistantProtocolException("The Home Assistant Energy information was malformed.");
        }

        var result = HomeAssistantJson.DeserializeResponse<HomeAssistantEnergyInfo>(value, "The Home Assistant Energy information could not be decoded.");
        return result;
    }

    /// <summary>Returns Home Assistant's complete Energy validation result without discarding provider-specific fields.</summary>
    public Task<JsonElement> ValidateAsync(CancellationToken cancellationToken = default)
        => _webSocket.RequestAsync("energy/validate", null, cancellationToken);

    /// <summary>Returns solar forecasts keyed by configuration-entry identifier.</summary>
    public async Task<IReadOnlyDictionary<string, JsonElement>> GetSolarForecastAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("energy/solar_forecast", null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("The Home Assistant solar forecast was not an object.");
        }

        return value.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.Clone(),
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<HomeAssistantFossilEnergyPeriod>> GetFossilEnergyConsumptionAsync(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyCollection<string> energyStatisticIds,
        string co2StatisticId,
        HomeAssistantEnergyPeriod period,
        CancellationToken cancellationToken = default)
    {
        if (end <= start) throw new ArgumentOutOfRangeException(nameof(end), "The end must be after the start.");
        var ids = RequireIds(energyStatisticIds, nameof(energyStatisticIds));
        if (string.IsNullOrWhiteSpace(co2StatisticId)) throw new ArgumentException("A CO2 statistic identifier is required.", nameof(co2StatisticId));
        var value = await _webSocket.RequestAsync("energy/fossil_energy_consumption", new Dictionary<string, object?>
        {
            ["start_time"] = start.ToString("O", CultureInfo.InvariantCulture),
            ["end_time"] = end.ToString("O", CultureInfo.InvariantCulture),
            ["energy_statistic_ids"] = ids,
            ["co2_statistic_id"] = co2StatisticId.Trim(),
            ["period"] = PeriodName(period)
        }, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("The Home Assistant fossil-energy response was not an object.");
        }

        var result = new List<HomeAssistantFossilEnergyPeriod>();
        foreach (var property in value.EnumerateObject())
        {
            if (!HomeAssistantTimestamp.TryParse(property.Name, out var timestamp)
                || property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetDouble(out var amount)
                || double.IsNaN(amount)
                || double.IsInfinity(amount))
            {
                throw new HomeAssistantProtocolException("The Home Assistant fossil-energy response contained an invalid period.");
            }

            result.Add(new HomeAssistantFossilEnergyPeriod { Start = timestamp, EnergyKiloWattHours = amount });
        }

        return result.OrderBy(item => item.Start).ToArray();
    }

    private static string[] RequireIds(IReadOnlyCollection<string> values, string parameterName)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one non-empty statistic identifier is required.", parameterName);
        return values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void ValidatePreferences(HomeAssistantEnergyPreferences preferences)
    {
        RequireObjectArray(preferences.EnergySources, "energy_sources");
        RequireObjectArray(preferences.DeviceConsumption, "device_consumption");
        RequireObjectArray(preferences.DeviceConsumptionWater, "device_consumption_water", required: false);
    }

    private static void RequireObjectArray(JsonElement value, string name, bool required = true)
    {
        if (!required && value.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Array
            || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object))
        {
            throw new HomeAssistantProtocolException($"The Home Assistant {name} preference collection was malformed.");
        }
    }

    private static string PeriodName(HomeAssistantEnergyPeriod value) => value switch
    {
        HomeAssistantEnergyPeriod.FiveMinute => "5minute",
        HomeAssistantEnergyPeriod.Hour => "hour",
        HomeAssistantEnergyPeriod.Day => "day",
        HomeAssistantEnergyPeriod.Month => "month",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
