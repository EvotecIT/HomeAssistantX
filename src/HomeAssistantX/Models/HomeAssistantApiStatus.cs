using System.Text.Json.Serialization;

namespace HomeAssistantX.Models;

public sealed class HomeAssistantApiStatus
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
