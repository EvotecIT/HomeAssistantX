using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Models;

/// <summary>Common fields returned by <c>/api/config</c>, with unknown fields preserved.</summary>
public sealed class HomeAssistantConfiguration
{
    [JsonPropertyName("location_name")]
    public string? LocationName { get; set; }

    [JsonPropertyName("time_zone")]
    public string? TimeZone { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("components")]
    public string[] Components { get; set; } = Array.Empty<string>();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}
