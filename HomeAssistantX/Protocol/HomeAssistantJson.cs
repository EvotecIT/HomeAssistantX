using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Exceptions;

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

    public static JsonSerializerOptions RawSerializerOptions { get; } = new(SerializerOptions)
    {
        PropertyNameCaseInsensitive = true
    };

    public static JsonElement Clone(JsonElement value)
    {
        return value.Clone();
    }

    /// <summary>Parses a Home Assistant response while preserving the classified protocol-failure contract.</summary>
    public static JsonDocument ParseResponse(string value, string failureMessage)
    {
        try
        {
            return JsonDocument.Parse(value);
        }
        catch (JsonException ex)
        {
            throw new HomeAssistantProtocolException(failureMessage, ex);
        }
    }

    /// <summary>Decodes a built-in Home Assistant response while preserving the classified protocol-failure contract.</summary>
    public static T DeserializeResponse<T>(JsonElement value, string failureMessage)
    {
        try
        {
            return value.Deserialize<T>(SerializerOptions)
                ?? throw new HomeAssistantProtocolException(failureMessage);
        }
        catch (JsonException ex)
        {
            throw new HomeAssistantProtocolException(failureMessage, ex);
        }
    }
}
