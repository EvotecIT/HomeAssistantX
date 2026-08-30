using System.Text.Json;

namespace HomeAssistantX.Protocol;

internal static class CancellationAwareJsonValueReader
{
    private const int CopyChunkLength = 16 * 1024;

    internal static string ReadString(ref Utf8JsonReader reader, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (reader.TokenType is not JsonTokenType.String and not JsonTokenType.PropertyName)
            throw new JsonException("A JSON string token was required.");

        var sequenceLength = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
        if (sequenceLength > int.MaxValue - 2)
            throw new JsonException("A JSON string token exceeded the supported response size.");

        var payload = new byte[checked((int)sequenceLength + 2)];
        payload[0] = (byte)'"';
        var offset = 1;
        if (reader.HasValueSequence)
        {
            foreach (var segment in reader.ValueSequence)
                CopyBytes(segment.Span, payload, ref offset, cancellationToken);
        }
        else
        {
            CopyBytes(reader.ValueSpan, payload, ref offset, cancellationToken);
        }
        payload[offset] = (byte)'"';

        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled || payload.Length <= CopyChunkLength)
        {
            var value = JsonSerializer.Deserialize<string>(payload)!;
            cancellationToken.ThrowIfCancellationRequested();
            return value;
        }

        var parseTask = Task.Run(() => JsonSerializer.Deserialize<string>(payload)!);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        var completed = Task.WhenAny(parseTask, canceled.Task).GetAwaiter().GetResult();
        if (!ReferenceEquals(completed, parseTask))
        {
            _ = parseTask.ContinueWith(
                task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return parseTask.GetAwaiter().GetResult();
    }

    internal static JsonElement Read(ref Utf8JsonReader reader, CancellationToken cancellationToken)
    {
        ArraySegment<byte> payload;
        using (var buffer = new MemoryStream())
        {
            Copy(ref reader, buffer, cancellationToken);
            if (!buffer.TryGetBuffer(out payload))
                throw new InvalidOperationException("The JSON value buffer could not be accessed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled)
        {
            using var document = JsonDocument.Parse(payload.AsMemory());
            return document.RootElement.Clone();
        }

        var parseTask = Task.Run(() =>
        {
            using var document = JsonDocument.Parse(payload.AsMemory());
            return document.RootElement.Clone();
        });
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        var completed = Task.WhenAny(parseTask, canceled.Task).GetAwaiter().GetResult();
        if (!ReferenceEquals(completed, parseTask))
        {
            _ = parseTask.ContinueWith(
                task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }
        return parseTask.GetAwaiter().GetResult();
    }

    private static void Copy(ref Utf8JsonReader reader, MemoryStream buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                buffer.WriteByte((byte)'{');
                var firstProperty = true;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException("A JSON object contained an invalid member.");
                    if (!firstProperty) buffer.WriteByte((byte)',');
                    WriteStringToken(ref reader, buffer, cancellationToken);
                    buffer.WriteByte((byte)':');
                    if (!reader.Read()) throw new JsonException("A JSON object was incomplete.");
                    Copy(ref reader, buffer, cancellationToken);
                    firstProperty = false;
                }
                if (reader.TokenType != JsonTokenType.EndObject) throw new JsonException("A JSON object was incomplete.");
                buffer.WriteByte((byte)'}');
                return;
            case JsonTokenType.StartArray:
                buffer.WriteByte((byte)'[');
                var firstElement = true;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!firstElement) buffer.WriteByte((byte)',');
                    Copy(ref reader, buffer, cancellationToken);
                    firstElement = false;
                }
                if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("A JSON array was incomplete.");
                buffer.WriteByte((byte)']');
                return;
            case JsonTokenType.String:
                WriteStringToken(ref reader, buffer, cancellationToken);
                return;
            case JsonTokenType.Number:
                WriteTokenBytes(ref reader, buffer, cancellationToken);
                return;
            case JsonTokenType.True:
                WriteAscii(buffer, "true", cancellationToken);
                return;
            case JsonTokenType.False:
                WriteAscii(buffer, "false", cancellationToken);
                return;
            case JsonTokenType.Null:
                WriteAscii(buffer, "null", cancellationToken);
                return;
            default:
                throw new JsonException("A JSON value contained an invalid token.");
        }
    }

    private static void WriteStringToken(ref Utf8JsonReader reader, MemoryStream buffer, CancellationToken cancellationToken)
    {
        buffer.WriteByte((byte)'"');
        WriteTokenBytes(ref reader, buffer, cancellationToken);
        buffer.WriteByte((byte)'"');
    }

    private static void WriteTokenBytes(ref Utf8JsonReader reader, MemoryStream buffer, CancellationToken cancellationToken)
    {
        if (reader.HasValueSequence)
        {
            foreach (var segment in reader.ValueSequence)
                WriteBytes(segment.Span, buffer, cancellationToken);
        }
        else
        {
            WriteBytes(reader.ValueSpan, buffer, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void WriteBytes(ReadOnlySpan<byte> source, MemoryStream buffer, CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < source.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(CopyChunkLength, source.Length - offset);
            var chunk = source.Slice(offset, count).ToArray();
            buffer.Write(chunk, 0, chunk.Length);
            offset += count;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void WriteAscii(MemoryStream buffer, string value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        for (var index = 0; index < value.Length; index++) buffer.WriteByte((byte)value[index]);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void CopyBytes(
        ReadOnlySpan<byte> source,
        byte[] destination,
        ref int destinationOffset,
        CancellationToken cancellationToken)
    {
        for (var sourceOffset = 0; sourceOffset < source.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = Math.Min(CopyChunkLength, source.Length - sourceOffset);
            source.Slice(sourceOffset, count).CopyTo(destination.AsSpan(destinationOffset, count));
            sourceOffset += count;
            destinationOffset += count;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}
