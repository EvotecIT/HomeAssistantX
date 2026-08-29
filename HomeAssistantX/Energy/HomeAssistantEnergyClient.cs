using System.Globalization;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Recorder;
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
        RequireNoDuplicatePreferenceProperties(value, cancellationToken);
        var preferences = HomeAssistantJson.DeserializeResponse<HomeAssistantEnergyPreferences>(
            value,
            "The Home Assistant Energy preferences could not be decoded.",
            cancellationToken: cancellationToken);
        ValidatePreferences(preferences, cancellationToken);
        return preferences;
    }

    public async Task<HomeAssistantEnergyPreferences> SavePreferencesAsync(
        HomeAssistantEnergyPreferencesUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        cancellationToken.ThrowIfCancellationRequested();
        var value = await _webSocket.RequestAsync("energy/save_prefs", update.ToPayload(cancellationToken), cancellationToken).ConfigureAwait(false);
        RequireNoDuplicatePreferenceProperties(value, cancellationToken);
        var preferences = HomeAssistantJson.DeserializeResponse<HomeAssistantEnergyPreferences>(
            value,
            "The updated Home Assistant Energy preferences could not be decoded.",
            cancellationToken: cancellationToken);
        ValidatePreferences(preferences, cancellationToken);
        return preferences;
    }

    public async Task<HomeAssistantEnergyInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("energy/info", null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object
            || HomeAssistantJson.HasDuplicateProperties(value, cancellationToken)
            || !value.TryGetProperty("cost_sensors", out var costSensors)
            || costSensors.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("solar_forecast_domains", out var forecastDomains)
            || forecastDomains.ValueKind != JsonValueKind.Array
            || !HasCanonicalUniqueDomains(forecastDomains, cancellationToken))
        {
            throw new HomeAssistantProtocolException("The Home Assistant Energy information was malformed.");
        }

        var result = HomeAssistantJson.DeserializeResponse<HomeAssistantEnergyInfo>(
            value,
            "The Home Assistant Energy information could not be decoded.",
            cancellationToken: cancellationToken);
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

        var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(property.Name)
                || !string.Equals(property.Name, property.Name.Trim(), StringComparison.Ordinal)
                || property.Value.ValueKind != JsonValueKind.Object)
            {
                throw new HomeAssistantProtocolException("The Home Assistant solar forecast contained an invalid entry.");
            }

            if (result.ContainsKey(property.Name))
            {
                throw new HomeAssistantProtocolException("The Home Assistant solar forecast contained a duplicate configuration-entry identifier.");
            }

            result.Add(property.Name, HomeAssistantJson.DeserializeResponse<JsonElement>(
                property.Value,
                "A Home Assistant solar-forecast entry could not be decoded.",
                cancellationToken: cancellationToken));
        }

        return result;
    }

    private static bool HasCanonicalUniqueDomains(JsonElement domains, CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in domains.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.String
                || item.GetString() is not string value
                || !HomeAssistantEntityId.TryNormalizeDomain(value, out var normalized)
                || !string.Equals(value, normalized, StringComparison.Ordinal)
                || !seen.Add(normalized))
            {
                return false;
            }
        }

        return true;
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
        var ids = RequireIds(energyStatisticIds, nameof(energyStatisticIds), cancellationToken);
        var normalizedCo2StatisticId = RequireStatisticId(co2StatisticId, nameof(co2StatisticId));
        var value = await _webSocket.RequestAsync("energy/fossil_energy_consumption", new Dictionary<string, object?>
        {
            ["start_time"] = start.ToString("O", CultureInfo.InvariantCulture),
            ["end_time"] = end.ToString("O", CultureInfo.InvariantCulture),
            ["energy_statistic_ids"] = ids,
            ["co2_statistic_id"] = normalizedCo2StatisticId,
            ["period"] = PeriodName(period)
        }, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("The Home Assistant fossil-energy response was not an object.");
        }

        var result = new List<HomeAssistantFossilEnergyPeriod>();
        var starts = new HashSet<long>();
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantTimestamp.TryParse(property.Name, out var timestamp)
                || !IsWithinRequestedWindow(timestamp, start, end, period)
                || period == HomeAssistantEnergyPeriod.Hour && !IsUtcHourBoundary(timestamp)
                || property.Value.ValueKind != JsonValueKind.Number
                || !property.Value.TryGetDouble(out var amount)
                || double.IsNaN(amount)
                || double.IsInfinity(amount))
            {
                throw new HomeAssistantProtocolException("The Home Assistant fossil-energy response contained an invalid period.");
            }

            if (!starts.Add(timestamp.UtcDateTime.Ticks))
            {
                throw new HomeAssistantProtocolException("The Home Assistant fossil-energy response contained a duplicate period.");
            }

            result.Add(new HomeAssistantFossilEnergyPeriod { Start = timestamp, EnergyKiloWattHours = amount });
        }

        cancellationToken.ThrowIfCancellationRequested();
        SortFossilEnergyPeriods(result, (left, right) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return left.Start.CompareTo(right.Start);
        });
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static bool IsUtcHourBoundary(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return utc.Minute == 0
            && utc.Second == 0
            && utc.Millisecond == 0
            && utc.Ticks % TimeSpan.TicksPerMillisecond == 0;
    }

    internal static void SortFossilEnergyPeriods(
        List<HomeAssistantFossilEnergyPeriod> periods,
        Comparison<HomeAssistantFossilEnergyPeriod> comparison)
    {
        CancellationAwareSort.Sort(periods, comparison);
    }

    private static bool IsWithinRequestedWindow(
        DateTimeOffset timestamp,
        DateTimeOffset start,
        DateTimeOffset end,
        HomeAssistantEnergyPeriod period)
    {
        var earliest = period switch
        {
            // Home Assistant 2026.8 accepts `5minute` but currently reduces every
            // period other than hour/day into calendar-month buckets.
            HomeAssistantEnergyPeriod.FiveMinute => start.AddDays(-33),
            HomeAssistantEnergyPeriod.Hour => start.AddHours(-1),
            HomeAssistantEnergyPeriod.Day => start.AddHours(-27),
            HomeAssistantEnergyPeriod.Month => start.AddDays(-33),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        return timestamp >= earliest && timestamp < end;
    }

    private static string[] RequireIds(
        IReadOnlyCollection<string> values,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (values is null || values.Count == 0)
            throw new ArgumentException("At least one statistic identifier is required.", parameterName);
        var normalized = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var identifier = RequireStatisticId(value, parameterName);
            if (!seen.Add(identifier))
                throw new ArgumentException("Statistic identifiers must be unique.", parameterName);
            normalized.Add(identifier);
        }

        return normalized.ToArray();
    }

    private static string RequireStatisticId(string value, string parameterName)
    {
        if (!HomeAssistantStatisticIdentifier.TryNormalize(value, out var normalized))
            throw new ArgumentException("A canonical statistic identifier is required.", parameterName);
        return normalized;
    }

    private static void ValidatePreferences(
        HomeAssistantEnergyPreferences preferences,
        CancellationToken cancellationToken)
    {
        RequireObjectArray(preferences.EnergySources, "energy_sources", cancellationToken);
        RequireObjectArray(preferences.DeviceConsumption, "device_consumption", cancellationToken);
        RequireObjectArray(preferences.DeviceConsumptionWater, "device_consumption_water", cancellationToken, required: false);
    }

    private static void RequireNoDuplicatePreferenceProperties(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        if (HomeAssistantJson.HasDuplicateProperties(value, cancellationToken))
            throw new HomeAssistantProtocolException("The Home Assistant Energy preferences contained duplicate JSON properties.");
    }

    private static void RequireObjectArray(
        JsonElement value,
        string name,
        CancellationToken cancellationToken,
        bool required = true)
    {
        if (!required && value.ValueKind == JsonValueKind.Undefined)
        {
            return;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantProtocolException($"The Home Assistant {name} preference collection was malformed.");
        }

        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new HomeAssistantProtocolException($"The Home Assistant {name} preference collection was malformed.");
            }

            if (!HomeAssistantEnergyPreferencesUpdate.HasRequiredIdentity(item, name))
            {
                throw new HomeAssistantProtocolException(
                    $"The Home Assistant {name} preference collection omitted a required canonical identity field.");
            }
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
