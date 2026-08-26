using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Protocol;

internal static class HomeAssistantJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static JsonElement Clone(JsonElement value)
    {
        return value.Clone();
    }
}
