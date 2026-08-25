using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Models;

/// <summary>A message published by the Home Assistant event bus.</summary>
public sealed class HomeAssistantEvent
{
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public Dictionary<string, JsonElement> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("origin")]
    public string? Origin { get; set; }

    [JsonPropertyName("time_fired")]
    public DateTimeOffset? TimeFired { get; set; }

    [JsonPropertyName("context")]
    public HomeAssistantContext? Context { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
