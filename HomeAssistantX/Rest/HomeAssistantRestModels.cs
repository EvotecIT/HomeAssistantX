using System.Text.Json;
using System.Buffers;
using System.Text.Json.Serialization;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Rest;

/// <summary>An event type currently registered on the Home Assistant event bus.</summary>
public sealed class HomeAssistantEventType
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("listener_count")]
    public int ListenerCount { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>The state and attributes used to create or update a state representation.</summary>
public sealed class HomeAssistantStateUpdate
{
    public HomeAssistantStateUpdate(string state)
    {
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("A state value is required.", nameof(state));
        }

        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    [JsonPropertyName("state")]
    public string State { get; }

    [JsonPropertyName("attributes")]
    public IReadOnlyDictionary<string, object?>? Attributes { get; set; }
}

/// <summary>The result of validating the active Home Assistant configuration.</summary>
public sealed class HomeAssistantConfigurationCheck
{
    [JsonPropertyName("result")]
    public string Result { get; set; } = string.Empty;

    [JsonPropertyName("errors")]
    public string? Errors { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore]
    public bool IsValid => string.Equals(Result, "valid", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A Home Assistant calendar entity.</summary>
public sealed class HomeAssistantCalendar
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A date or date-time boundary returned for a calendar event.</summary>
[JsonConverter(typeof(HomeAssistantCalendarBoundaryJsonConverter))]
public sealed class HomeAssistantCalendarBoundary
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("dateTime")]
    public DateTimeOffset? DateTime { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>An event returned by a Home Assistant calendar entity.</summary>
[JsonConverter(typeof(HomeAssistantCalendarEventJsonConverter))]
public sealed class HomeAssistantCalendarEvent
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public HomeAssistantCalendarBoundary? Start { get; set; }

    [JsonPropertyName("end")]
    public HomeAssistantCalendarBoundary? End { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("uid")]
    public string? Uid { get; set; }

    [JsonPropertyName("recurrence_id")]
    public string? RecurrenceId { get; set; }

    [JsonPropertyName("rrule")]
    public string? RecurrenceRule { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class HomeAssistantCalendarEventJsonConverter : JsonConverter<HomeAssistantCalendarEvent>
{
    private readonly CancellationToken _cancellationToken;

    public HomeAssistantCalendarEventJsonConverter()
        : this(default)
    {
    }

    internal HomeAssistantCalendarEventJsonConverter(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public override HomeAssistantCalendarEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A Home Assistant calendar event must be an object.");
        }

        var result = new HomeAssistantCalendarEvent();
        var propertyNames = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("A Home Assistant calendar event contained an invalid object member.");
            }

            var propertyName = HomeAssistantCancellationJsonValueReader.ReadString(ref reader, _cancellationToken);
            if (ContainsOrdinal(propertyNames, propertyName, _cancellationToken))
            {
                throw new JsonException("A Home Assistant calendar event contained a duplicate field.");
            }
            propertyNames.Add(propertyName);

            if (!reader.Read())
            {
                throw new JsonException("A Home Assistant calendar event ended before its property value.");
            }

            _cancellationToken.ThrowIfCancellationRequested();
            if (CancellationAwareString.EqualsOrdinal(propertyName, "summary", _cancellationToken))
            {
                result.Summary = ReadRequiredString(ref reader, "summary");
            }
            else if (CancellationAwareString.EqualsOrdinal(propertyName, "start", _cancellationToken))
            {
                result.Start = ReadBoundary(ref reader, options);
            }
            else if (CancellationAwareString.EqualsOrdinal(propertyName, "end", _cancellationToken))
            {
                result.End = ReadBoundary(ref reader, options);
            }
            else if (CancellationAwareString.EqualsOrdinal(propertyName, "description", _cancellationToken))
            {
                result.Description = ReadOptionalString(ref reader, "description");
            }
            else if (CancellationAwareString.EqualsOrdinal(propertyName, "location", _cancellationToken))
            {
                result.Location = ReadOptionalString(ref reader, "location");
            }
            else if (CancellationAwareString.EqualsOrdinal(propertyName, "uid", _cancellationToken))
            {
                result.Uid = ReadOptionalString(ref reader, "uid");
            }
            else if (CancellationAwareString.EqualsOrdinal(propertyName, "recurrence_id", _cancellationToken))
            {
                result.RecurrenceId = ReadOptionalString(ref reader, "recurrence_id");
            }
            else if (CancellationAwareString.EqualsOrdinal(propertyName, "rrule", _cancellationToken))
            {
                result.RecurrenceRule = ReadOptionalString(ref reader, "rrule");
            }
            else
            {
                HomeAssistantCancellationJsonValueReader.AddExtensionData(
                    result.AdditionalData,
                    propertyName,
                    HomeAssistantCancellationJsonValueReader.Read(ref reader, _cancellationToken),
                    _cancellationToken);
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("A Home Assistant calendar event object was incomplete.");
        }

        _cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private string ReadRequiredString(ref Utf8JsonReader reader, string propertyName)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A Home Assistant calendar " + propertyName + " must be a string.");
        }

        return HomeAssistantCancellationJsonValueReader.ReadString(ref reader, _cancellationToken);
    }

    private string? ReadOptionalString(ref Utf8JsonReader reader, string propertyName)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return ReadRequiredString(ref reader, propertyName);
    }

    private static HomeAssistantCalendarBoundary? ReadBoundary(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<HomeAssistantCalendarBoundary>(ref reader, options);
    }

    public override void Write(Utf8JsonWriter writer, HomeAssistantCalendarEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("summary", value.Summary);
        if (value.Start is not null)
        {
            writer.WritePropertyName("start");
            JsonSerializer.Serialize(writer, value.Start, options);
        }
        if (value.End is not null)
        {
            writer.WritePropertyName("end");
            JsonSerializer.Serialize(writer, value.End, options);
        }
        WriteOptionalString(writer, "description", value.Description);
        WriteOptionalString(writer, "location", value.Location);
        WriteOptionalString(writer, "uid", value.Uid);
        WriteOptionalString(writer, "recurrence_id", value.RecurrenceId);
        WriteOptionalString(writer, "rrule", value.RecurrenceRule);
        foreach (var pair in value.AdditionalData)
        {
            writer.WritePropertyName(pair.Key);
            pair.Value.WriteTo(writer);
        }
        writer.WriteEndObject();
    }

    private static void WriteOptionalString(Utf8JsonWriter writer, string propertyName, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, value);
        }
    }

    private static bool ContainsOrdinal(
        IReadOnlyList<string> values,
        string candidate,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < values.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancellationAwareString.EqualsOrdinal(values[index], candidate, cancellationToken)) return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }
}

internal static class HomeAssistantCancellationJsonValueReader
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
        var valueLength = (int)sequenceLength;
        var payload = new byte[checked(valueLength + 2)];
        payload[0] = (byte)'"';
        var offset = 1;
        if (reader.HasValueSequence)
        {
            foreach (var segment in reader.ValueSequence)
            {
                CopyBytes(segment.Span, payload, ref offset, cancellationToken);
            }
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
                throw new InvalidOperationException("The extension JSON buffer could not be accessed.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled)
        {
            var document = JsonDocument.Parse(payload.AsMemory());
            return document.RootElement;
        }

        var parseTask = Task.Run(() =>
        {
            var document = JsonDocument.Parse(payload.AsMemory());
            return document.RootElement;
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

    internal static void AddExtensionData(
        Dictionary<string, JsonElement> target,
        string propertyName,
        JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationAwareString.Observe(propertyName, cancellationToken);
        if (!cancellationToken.CanBeCanceled || propertyName.Length <= CopyChunkLength)
        {
            target.Add(propertyName, value);
            cancellationToken.ThrowIfCancellationRequested();
            return;
        }

        var addTask = Task.Run(() => target.Add(propertyName, value));
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        var completed = Task.WhenAny(addTask, canceled.Task).GetAwaiter().GetResult();
        if (!ReferenceEquals(completed, addTask))
        {
            _ = addTask.ContinueWith(
                task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        addTask.GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
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
                        throw new JsonException("An extension object contained an invalid member.");
                    if (!firstProperty) buffer.WriteByte((byte)',');
                    WriteStringToken(ref reader, buffer, cancellationToken);
                    buffer.WriteByte((byte)':');
                    if (!reader.Read()) throw new JsonException("An extension object was incomplete.");
                    Copy(ref reader, buffer, cancellationToken);
                    firstProperty = false;
                }
                if (reader.TokenType != JsonTokenType.EndObject)
                    throw new JsonException("An extension object was incomplete.");
                buffer.WriteByte((byte)'}');
                return;
            case JsonTokenType.StartArray:
                buffer.WriteByte((byte)'[');
                var firstElement = true;
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (!firstElement) buffer.WriteByte((byte)',');
                    Copy(ref reader, buffer, cancellationToken);
                    firstElement = false;
                }
                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("An extension array was incomplete.");
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
                throw new JsonException("An extension value contained an invalid JSON token.");
        }
    }

    private static void WriteStringToken(
        ref Utf8JsonReader reader,
        MemoryStream buffer,
        CancellationToken cancellationToken)
    {
        buffer.WriteByte((byte)'"');
        WriteTokenBytes(ref reader, buffer, cancellationToken);
        buffer.WriteByte((byte)'"');
    }

    private static void WriteTokenBytes(
        ref Utf8JsonReader reader,
        MemoryStream buffer,
        CancellationToken cancellationToken)
    {
        if (reader.HasValueSequence)
        {
            foreach (var segment in reader.ValueSequence)
            {
                WriteBytes(segment.Span, buffer, cancellationToken);
            }
        }
        else
        {
            WriteBytes(reader.ValueSpan, buffer, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void WriteBytes(
        ReadOnlySpan<byte> source,
        MemoryStream buffer,
        CancellationToken cancellationToken)
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

    private static void WriteAscii(
        MemoryStream buffer,
        string value,
        CancellationToken cancellationToken)
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

internal sealed class HomeAssistantCalendarBoundaryJsonConverter : JsonConverter<HomeAssistantCalendarBoundary>
{
    private const int MaximumBoundaryTextLength = 128;
    private readonly CancellationToken _cancellationToken;

    public HomeAssistantCalendarBoundaryJsonConverter()
        : this(default)
    {
    }

    internal HomeAssistantCalendarBoundaryJsonConverter(CancellationToken cancellationToken)
    {
        _cancellationToken = cancellationToken;
    }

    public override HomeAssistantCalendarBoundary? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = HomeAssistantCancellationJsonValueReader.ReadString(ref reader, _cancellationToken);
            if (value.Length > MaximumBoundaryTextLength
                || CancellationAwareString.IsNullOrWhiteSpace(value, _cancellationToken))
            {
                throw new JsonException("A Home Assistant calendar boundary string cannot be blank.");
            }

            if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out _))
            {
                return new HomeAssistantCalendarBoundary { Date = value };
            }

            if (TryReadWireDateTime(value, out var dateTime))
            {
                return new HomeAssistantCalendarBoundary { DateTime = dateTime };
            }

            throw new JsonException(
                "A Home Assistant calendar boundary string must be an ISO date or an offset timestamp.");
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("A Home Assistant calendar boundary must be a string or object.");
        }

        string? dateValue = null;
        DateTimeOffset? parsedDateTime = null;
        var hasDate = false;
        var hasDateTime = false;
        var additionalData = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var propertyNames = new List<string>();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("A Home Assistant calendar boundary contained an invalid object member.");
            }

            var propertyName = HomeAssistantCancellationJsonValueReader.ReadString(ref reader, _cancellationToken);
            if (ContainsOrdinal(propertyNames, propertyName, _cancellationToken))
            {
                throw new JsonException("A Home Assistant calendar boundary contained a duplicate field.");
            }
            propertyNames.Add(propertyName);

            if (!reader.Read())
            {
                throw new JsonException("A Home Assistant calendar boundary ended before its property value.");
            }

            _cancellationToken.ThrowIfCancellationRequested();
            if (CancellationAwareString.EqualsOrdinal(propertyName, "date", _cancellationToken))
            {
                hasDate = true;
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("A Home Assistant calendar date must be a string.");
                }

                dateValue = HomeAssistantCancellationJsonValueReader.ReadString(ref reader, _cancellationToken);
                if (dateValue.Length > MaximumBoundaryTextLength
                    || !DateTime.TryParseExact(
                        dateValue,
                        "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out _))
                {
                    throw new JsonException("A Home Assistant calendar date must use yyyy-MM-dd.");
                }
            }
            else if (CancellationAwareString.EqualsOrdinal(propertyName, "dateTime", _cancellationToken))
            {
                hasDateTime = true;
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("A Home Assistant calendar dateTime must be a valid timestamp string.");
                }

                var dateTimeText = HomeAssistantCancellationJsonValueReader.ReadString(ref reader, _cancellationToken);
                if (dateTimeText.Length > MaximumBoundaryTextLength
                    || !TryReadWireDateTime(dateTimeText, out var dateTime))
                    throw new JsonException("A Home Assistant calendar dateTime must be a valid timestamp string.");

                parsedDateTime = dateTime;
            }
            else
            {
                HomeAssistantCancellationJsonValueReader.AddExtensionData(
                    additionalData,
                    propertyName,
                    HomeAssistantCancellationJsonValueReader.Read(ref reader, _cancellationToken),
                    _cancellationToken);
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("A Home Assistant calendar boundary object was incomplete.");
        }

        if (hasDate == hasDateTime)
        {
            throw new JsonException(
                "A Home Assistant calendar boundary must contain exactly one of date or dateTime.");
        }

        _cancellationToken.ThrowIfCancellationRequested();
        return new HomeAssistantCalendarBoundary
        {
            Date = dateValue,
            DateTime = parsedDateTime,
            AdditionalData = additionalData
        };
    }

    private static bool TryReadWireDateTime(JsonElement value, out DateTimeOffset result)
    {
        result = default;
        if (value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        return TryReadWireDateTime(value.GetString(), out result);
    }

    private static bool TryReadWireDateTime(string? text, out DateTimeOffset result)
    {
        return HomeAssistantTimestamp.TryParse(text, out result);
    }

    private static bool ContainsOrdinal(
        IReadOnlyList<string> values,
        string candidate,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < values.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancellationAwareString.EqualsOrdinal(values[index], candidate, cancellationToken)) return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    public override void Write(Utf8JsonWriter writer, HomeAssistantCalendarBoundary value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if (value.Date is not null)
        {
            writer.WriteString("date", value.Date);
        }
        else if (value.DateTime is not null)
        {
            writer.WriteString("dateTime", value.DateTime.Value);
        }

        foreach (var pair in value.AdditionalData)
        {
            writer.WritePropertyName(pair.Key);
            pair.Value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}

/// <summary>An entry returned by the Home Assistant logbook API.</summary>
public sealed class HomeAssistantLogbookEntry
{
    [JsonPropertyName("when")]
    public DateTimeOffset? When { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("domain")]
    public string? Domain { get; set; }

    [JsonPropertyName("entity_id")]
    public string? EntityId { get; set; }

    [JsonPropertyName("context_user_id")]
    public string? ContextUserId { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Filters a history query while preserving Home Assistant's performance switches.</summary>
public sealed class HomeAssistantHistoryQuery
{
    public HomeAssistantHistoryQuery(params string[] entityIds)
    {
        if (entityIds is null || entityIds.Length == 0 || entityIds.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one entity identifier is required.", nameof(entityIds));
        }

        var normalized = new string[entityIds.Length];
        for (var index = 0; index < entityIds.Length; index++)
        {
            if (!HomeAssistantEntityId.TryNormalize(entityIds[index], out normalized[index]))
            {
                throw new ArgumentException("History filters require valid Home Assistant entity identifiers.", nameof(entityIds));
            }
        }

        EntityIds = normalized;
    }

    public IReadOnlyList<string> EntityIds { get; }

    public DateTimeOffset? StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public bool MinimalResponse { get; set; }

    public bool NoAttributes { get; set; }

    public bool SignificantChangesOnly { get; set; }
}

/// <summary>Filters a Home Assistant logbook query.</summary>
public sealed class HomeAssistantLogbookQuery
{
    public DateTimeOffset? StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public string? EntityId { get; set; }
}
