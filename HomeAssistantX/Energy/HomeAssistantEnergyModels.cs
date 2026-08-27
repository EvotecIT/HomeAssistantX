using System.Text.Json;
using System.Text.Json.Serialization;

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

    internal Dictionary<string, object?> ToPayload()
    {
        var payload = new Dictionary<string, object?>();
        AddArray(payload, "energy_sources", EnergySources);
        AddArray(payload, "device_consumption", DeviceConsumption);
        AddArray(payload, "device_consumption_water", DeviceConsumptionWater);
        if (payload.Count == 0)
        {
            throw new ArgumentException("At least one Energy preference collection must be supplied.");
        }

        return payload;
    }

    private static void AddArray(IDictionary<string, object?> payload, string name, JsonElement? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        if (value.Value.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException($"The {name} preference must be a JSON array.", name);
        }

        if (value.Value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object))
        {
            throw new ArgumentException($"Every {name} preference entry must be a JSON object.", name);
        }

        payload[name] = value.Value.Clone();
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
