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
    [JsonConverter(typeof(HomeAssistantAttributeDictionaryConverter))]
    public Dictionary<string, JsonElement> Attributes { get; set; } = new(StringComparer.Ordinal);

    [JsonPropertyName("last_changed")]
    public DateTimeOffset? LastChanged { get; set; }

    [JsonPropertyName("last_reported")]
    public DateTimeOffset? LastReported { get; set; }

    [JsonPropertyName("last_updated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("context")]
    public HomeAssistantContext? Context { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore]
    public string Domain
    {
        get
        {
            if (string.IsNullOrEmpty(EntityId)) return string.Empty;
            var separator = EntityId.IndexOf('.');
            return separator <= 0 ? string.Empty : EntityId.Substring(0, separator);
        }
    }

    public bool TryGetAttribute<T>(string name, out T? value)
    {
        if (HomeAssistantAttributeReader.TryGetValue(Attributes, name, out var raw))
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
