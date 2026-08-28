using System.Text.Json;
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

        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("A Home Assistant calendar boundary must be a string or object.");
        }

        var hasDate = root.TryGetProperty("date", out var date);
        var hasDateTime = root.TryGetProperty("dateTime", out var dateTimeValue);
        if (hasDate == hasDateTime)
        {
            throw new JsonException(
                "A Home Assistant calendar boundary must contain exactly one of date or dateTime.");
        }

        string? dateValue = null;
        if (hasDate)
        {
            if (date.ValueKind != JsonValueKind.String)
            {
                throw new JsonException("A Home Assistant calendar date must be a string.");
            }

            dateValue = date.GetString();
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

        DateTimeOffset? parsedDateTime = null;
        if (hasDateTime)
        {
            if (!TryReadWireDateTime(dateTimeValue, out var dateTime))
            {
                throw new JsonException("A Home Assistant calendar dateTime must be a valid timestamp string.");
            }

            parsedDateTime = dateTime;
        }

        var additionalData = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            _cancellationToken.ThrowIfCancellationRequested();
            if (property.Name == "date" || property.Name == "dateTime")
            {
                continue;
            }

            if (additionalData.ContainsKey(property.Name))
            {
                throw new JsonException("A Home Assistant calendar boundary contained a duplicate extension field.");
            }

            additionalData.Add(property.Name, property.Value.Clone());
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
