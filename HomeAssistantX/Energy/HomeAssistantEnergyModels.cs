using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Protocol;
using HomeAssistantX.Recorder;

namespace HomeAssistantX.Energy;

/// <summary>The Energy dashboard preferences returned by Home Assistant.</summary>
public sealed class HomeAssistantEnergyPreferences
{
    [JsonPropertyName("energy_sources")]
    public JsonElement EnergySources { get; set; }

    [JsonPropertyName("device_consumption")]
    public JsonElement DeviceConsumption { get; set; }

    [JsonPropertyName("device_consumption_water")]
    public JsonElement DeviceConsumptionWater { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A partial Energy dashboard preference update. Only populated properties are sent.</summary>
public sealed class HomeAssistantEnergyPreferencesUpdate
{
    public JsonElement? EnergySources { get; set; }

    public JsonElement? DeviceConsumption { get; set; }

    public JsonElement? DeviceConsumptionWater { get; set; }

    internal Dictionary<string, object?> ToPayload(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = new Dictionary<string, object?>();
        AddArray(payload, "energy_sources", EnergySources, cancellationToken);
        AddArray(payload, "device_consumption", DeviceConsumption, cancellationToken);
        AddArray(payload, "device_consumption_water", DeviceConsumptionWater, cancellationToken);
        if (payload.Count == 0)
        {
            throw new ArgumentException("At least one Energy preference collection must be supplied.");
        }

        return payload;
    }

    private static void AddArray(
        IDictionary<string, object?> payload,
        string name,
        JsonElement? value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!value.HasValue)
        {
            return;
        }

        var snapshot = HomeAssistantJson.FreezeValue(value.Value, name, name + " preference", cancellationToken);
        if (snapshot.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"The {name} preference must be a JSON array.", name);
        }
        if (HomeAssistantJson.HasDuplicateProperties(snapshot, cancellationToken))
        {
            throw new ArgumentException($"Every {name} preference entry must use each JSON property name only once.", name);
        }

        foreach (var item in snapshot.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"Every {name} preference entry must be a JSON object.", name);
            }

            if (!HasRequiredIdentity(item, name, cancellationToken))
            {
                throw new ArgumentException($"Every {name} preference entry must contain its canonical required identity field.", name);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        payload[name] = snapshot;
    }

    internal static bool HasRequiredIdentity(
        JsonElement item,
        string collectionName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinal(
            collectionName,
            "energy_sources",
            cancellationToken))
        {
            return item.TryGetProperty("type", out var type)
                && type.ValueKind == JsonValueKind.String
                && type.GetString() is string value
                && !HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken)
                && HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinal(
                    value,
                    HomeAssistantX.Protocol.CancellationAwareString.Trim(value, cancellationToken),
                    cancellationToken);
        }

        if (!item.TryGetProperty("stat_consumption", out var statistic)
            || statistic.ValueKind != JsonValueKind.String
            || statistic.GetString() is not string statisticId
            || !HomeAssistantStatisticIdentifier.TryNormalize(statisticId, cancellationToken, out var normalized))
        {
            return false;
        }

        return HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinal(
            statisticId,
            normalized,
            cancellationToken);
    }
}

/// <summary>Energy integration capabilities and provider-owned cost sensor metadata.</summary>
public sealed class HomeAssistantEnergyInfo
{
    [JsonPropertyName("cost_sensors")]
    public JsonElement CostSensors { get; set; }

    [JsonPropertyName("solar_forecast_domains")]
    public IReadOnlyList<string> SolarForecastDomains { get; set; } = Array.Empty<string>();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A fossil-energy amount calculated for one period.</summary>
public sealed class HomeAssistantFossilEnergyPeriod
{
    public DateTimeOffset Start { get; set; }

    public double EnergyKiloWattHours { get; set; }
}

/// <summary>Supported aggregation periods for fossil-energy calculations.</summary>
public enum HomeAssistantEnergyPeriod
{
    FiveMinute,
    Hour,
    Day,
    Month
}
