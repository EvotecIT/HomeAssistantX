using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Models;

internal sealed class HomeAssistantAttributeDictionaryConverter
    : JsonConverter<Dictionary<string, JsonElement>>
{
    private static readonly AsyncLocal<CancellationToken> CurrentCancellationToken = new();

    public override bool HandleNull => true;

    internal static IDisposable UseCancellationToken(CancellationToken cancellationToken)
    {
        var previous = CurrentCancellationToken.Value;
        CurrentCancellationToken.Value = cancellationToken;
        return new CancellationScope(previous);
    }

    public override Dictionary<string, JsonElement> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var cancellationToken = CurrentCancellationToken.Value;
        cancellationToken.ThrowIfCancellationRequested();
        if (reader.TokenType == JsonTokenType.Null)
        {
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            _ = CancellationAwareJsonValueReader.Read(ref reader, cancellationToken);
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        var attributes = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("A Home Assistant state attributes object contained an invalid member.");
            }

            var propertyName = CancellationAwareJsonValueReader.ReadString(ref reader, cancellationToken);
            if (attributes.ContainsKey(propertyName))
            {
                throw new JsonException("A Home Assistant state contained duplicate attribute properties.");
            }

            if (!reader.Read())
            {
                throw new JsonException("A Home Assistant state attributes object was incomplete.");
            }
            attributes.Add(
                propertyName,
                CancellationAwareJsonValueReader.Read(ref reader, cancellationToken));
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("A Home Assistant state attributes object was incomplete.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return attributes;
    }

    public override void Write(
        Utf8JsonWriter writer,
        Dictionary<string, JsonElement> value,
        JsonSerializerOptions options)
    {
        var cancellationToken = CurrentCancellationToken.Value;
        cancellationToken.ThrowIfCancellationRequested();
        writer.WriteStartObject();
        foreach (var attribute in value ?? Enumerable.Empty<KeyValuePair<string, JsonElement>>())
        {
            HomeAssistantJson.ThrowIfStringTraversalCanceled(attribute.Key, cancellationToken);
            writer.WritePropertyName(attribute.Key);
            HomeAssistantJson.WriteJsonElement(writer, attribute.Value, cancellationToken);
        }

        writer.WriteEndObject();
        cancellationToken.ThrowIfCancellationRequested();
    }

    private sealed class CancellationScope : IDisposable
    {
        private readonly CancellationToken _previous;
        private bool _disposed;

        internal CancellationScope(CancellationToken previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed) return;
            CurrentCancellationToken.Value = _previous;
            _disposed = true;
        }
    }
}
