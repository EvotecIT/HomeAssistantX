using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Rest;

/// <summary>An event type currently registered on the Home Assistant event bus.</summary>
public sealed class HomeAssistantEventType
{
    [JsonPropertyName("event")]
    public string Event { get; set; } = string.Empty;

    [JsonPropertyName("listener_count")]
    public int ListenerCount { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A date or date-time boundary returned for a calendar event.</summary>
public sealed class HomeAssistantCalendarBoundary
{
    [JsonPropertyName("date")]
    public string? Date { get; set; }

    [JsonPropertyName("dateTime")]
    public DateTimeOffset? DateTime { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
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

        EntityIds = entityIds.ToArray();
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
