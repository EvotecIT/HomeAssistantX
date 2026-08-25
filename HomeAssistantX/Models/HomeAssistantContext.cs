using System.Text.Json.Serialization;

namespace HomeAssistantX.Models;

/// <summary>Identifies the causal chain for a Home Assistant state change, event, or service call.</summary>
public sealed class HomeAssistantContext
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("parent_id")]
    public string? ParentId { get; set; }

    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
