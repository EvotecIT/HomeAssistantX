using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Models;

/// <summary>A provider-neutral representation of a raw Home Assistant entity state.</summary>
public sealed class HomeAssistantState
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public Dictionary<string, JsonElement> Attributes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("last_changed")]
    public DateTimeOffset? LastChanged { get; set; }

    [JsonPropertyName("last_reported")]
    public DateTimeOffset? LastReported { get; set; }

    [JsonPropertyName("last_updated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("context")]
    public HomeAssistantContext? Context { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonIgnore]
    public string Domain
    {
        get
        {
            var separator = EntityId.IndexOf('.');
            return separator <= 0 ? string.Empty : EntityId.Substring(0, separator);
        }
    }

    public bool TryGetAttribute<T>(string name, out T? value)
    {
        if (Attributes.TryGetValue(name, out var raw))
        {
            try
            {
                value = raw.Deserialize<T>(Protocol.HomeAssistantJson.RawSerializerOptions);
                return true;
            }
            catch (JsonException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        value = default;
        return false;
    }
}
