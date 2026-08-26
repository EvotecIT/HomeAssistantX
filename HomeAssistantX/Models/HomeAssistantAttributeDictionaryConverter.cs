using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Models;

internal sealed class HomeAssistantAttributeDictionaryConverter
    : JsonConverter<Dictionary<string, JsonElement>>
{
    public override bool HandleNull => true;

    public override Dictionary<string, JsonElement> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            attributes[property.Name] = property.Value.Clone();
        }

        return attributes;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, JsonElement> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var attribute in value)
        {
            writer.WritePropertyName(attribute.Key);
            attribute.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
