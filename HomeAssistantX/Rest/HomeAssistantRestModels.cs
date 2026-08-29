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
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("A Home Assistant calendar event contained an invalid object member.");
            }

            var propertyName = reader.GetString()!;
            if (!propertyNames.Add(propertyName))
            {
                throw new JsonException("A Home Assistant calendar event contained a duplicate field.");
            }

            if (!reader.Read())
            {
                throw new JsonException("A Home Assistant calendar event ended before its property value.");
            }

            _cancellationToken.ThrowIfCancellationRequested();
            switch (propertyName)
            {
                case "summary":
                    result.Summary = ReadRequiredString(ref reader, "summary");
                    break;
                case "start":
                    result.Start = ReadBoundary(ref reader, options);
                    break;
                case "end":
                    result.End = ReadBoundary(ref reader, options);
                    break;
                case "description":
                    result.Description = ReadOptionalString(ref reader, "description");
                    break;
                case "location":
                    result.Location = ReadOptionalString(ref reader, "location");
                    break;
                case "uid":
                    result.Uid = ReadOptionalString(ref reader, "uid");
                    break;
                case "recurrence_id":
                    result.RecurrenceId = ReadOptionalString(ref reader, "recurrence_id");
                    break;
                case "rrule":
                    result.RecurrenceRule = ReadOptionalString(ref reader, "rrule");
                    break;
                default:
                    result.AdditionalData.Add(
                        propertyName,
                        HomeAssistantCancellationJsonValueReader.Read(ref reader, _cancellationToken));
                    break;
            }
        }

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("A Home Assistant calendar event object was incomplete.");
        }

        _cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static string ReadRequiredString(ref Utf8JsonReader reader, string propertyName)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException("A Home Assistant calendar " + propertyName + " must be a string.");
        }

        return reader.GetString()!;
    }

    private static string? ReadOptionalString(ref Utf8JsonReader reader, string propertyName)
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
}

internal static class HomeAssistantCancellationJsonValueReader
{
    internal static JsonElement Read(ref Utf8JsonReader reader, CancellationToken cancellationToken)
    {
        byte[] payload;
        using (var buffer = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                Copy(ref reader, writer, cancellationToken);
                writer.Flush();
            }

            payload = buffer.ToArray();
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled)
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }

        var parseTask = Task.Run(() =>
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        });
        var completed = Task.WhenAny(parseTask, Task.Delay(Timeout.Infinite, cancellationToken))
            .GetAwaiter()
            .GetResult();
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

    private static void Copy(ref Utf8JsonReader reader, Utf8JsonWriter writer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (reader.TokenType)
        {
            case JsonTokenType.StartObject:
                writer.WriteStartObject();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (reader.TokenType != JsonTokenType.PropertyName)
                        throw new JsonException("An extension object contained an invalid member.");
                    writer.WritePropertyName(reader.GetString()!);
                    if (!reader.Read()) throw new JsonException("An extension object was incomplete.");
                    Copy(ref reader, writer, cancellationToken);
                }
                if (reader.TokenType != JsonTokenType.EndObject)
                    throw new JsonException("An extension object was incomplete.");
                writer.WriteEndObject();
                return;
            case JsonTokenType.StartArray:
                writer.WriteStartArray();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    Copy(ref reader, writer, cancellationToken);
                }
                if (reader.TokenType != JsonTokenType.EndArray)
                    throw new JsonException("An extension array was incomplete.");
                writer.WriteEndArray();
                return;
            case JsonTokenType.String:
                writer.WriteStringValue(reader.GetString());
                return;
            case JsonTokenType.Number:
                if (reader.HasValueSequence)
                    writer.WriteRawValue(reader.ValueSequence.ToArray(), skipInputValidation: true);
                else
                    writer.WriteRawValue(reader.ValueSpan, skipInputValidation: true);
                return;
            case JsonTokenType.True:
                writer.WriteBooleanValue(true);
                return;
            case JsonTokenType.False:
                writer.WriteBooleanValue(false);
                return;
            case JsonTokenType.Null:
                writer.WriteNullValue();
                return;
            default:
                throw new JsonException("An extension value contained an invalid JSON token.");
        }
    }
}

internal sealed class HomeAssistantCalendarBoundaryJsonConverter : JsonConverter<HomeAssistantCalendarBoundary>
{
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
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
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
        var propertyNames = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("A Home Assistant calendar boundary contained an invalid object member.");
            }

            var propertyName = reader.GetString()!;
            if (!propertyNames.Add(propertyName))
            {
                throw new JsonException("A Home Assistant calendar boundary contained a duplicate field.");
            }

            if (!reader.Read())
            {
                throw new JsonException("A Home Assistant calendar boundary ended before its property value.");
            }

            _cancellationToken.ThrowIfCancellationRequested();
            if (propertyName == "date")
            {
                hasDate = true;
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException("A Home Assistant calendar date must be a string.");
                }

                dateValue = reader.GetString();
                if (!DateTime.TryParseExact(
                        dateValue,
                        "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out _))
                {
                    throw new JsonException("A Home Assistant calendar date must use yyyy-MM-dd.");
                }
            }
            else if (propertyName == "dateTime")
            {
                hasDateTime = true;
                if (reader.TokenType != JsonTokenType.String
                    || !TryReadWireDateTime(reader.GetString(), out var dateTime))
                {
                    throw new JsonException("A Home Assistant calendar dateTime must be a valid timestamp string.");
                }

                parsedDateTime = dateTime;
            }
            else
            {
                additionalData.Add(
                    propertyName,
                    HomeAssistantCancellationJsonValueReader.Read(ref reader, _cancellationToken));
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
