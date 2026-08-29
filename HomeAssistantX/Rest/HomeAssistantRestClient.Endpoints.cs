using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using HomeAssistantX.Calendars;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Rest;

public sealed partial class HomeAssistantRestClient
{
    /// <summary>Gets the loaded Home Assistant components.</summary>
    public async Task<IReadOnlyList<string>> GetComponentsAsync(CancellationToken cancellationToken = default)
    {
        return await SendHomeAssistantAsync<string[]>(HttpMethod.Get, "api/components", null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the event types and current listener counts.</summary>
    public async Task<IReadOnlyList<HomeAssistantEventType>> GetEventTypesAsync(CancellationToken cancellationToken = default)
    {
        return await SendHomeAssistantAsync<HomeAssistantEventType[]>(HttpMethod.Get, "api/events", null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Gets recorded state history for one or more entities.</summary>
    public async Task<IReadOnlyList<IReadOnlyList<HomeAssistantState>>> GetHistoryAsync(
        HomeAssistantHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query is null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        ValidateOptionalTimeRange(query.StartTime, query.EndTime, nameof(query));

        var path = "api/history/period";
        if (query.StartTime.HasValue)
        {
            path += "/" + EscapeTimestamp(query.StartTime.Value);
        }

        var parameters = new List<KeyValuePair<string, string?>>
        {
            new("filter_entity_id", string.Join(",", query.EntityIds))
        };
        AddTimestamp(parameters, "end_time", query.EndTime);
        AddFlag(parameters, "minimal_response", query.MinimalResponse);
        AddFlag(parameters, "no_attributes", query.NoAttributes);
        AddFlag(parameters, "significant_changes_only", query.SignificantChangesOnly);

        var response = await SendHomeAssistantAsync<HomeAssistantState[][]>(
            HttpMethod.Get,
            AppendQuery(path, parameters),
            null,
            cancellationToken).ConfigureAwait(false);
        return response;
    }

    /// <summary>Gets entries from the Home Assistant logbook.</summary>
    public async Task<IReadOnlyList<HomeAssistantLogbookEntry>> GetLogbookAsync(
        HomeAssistantLogbookQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new HomeAssistantLogbookQuery();
        var startTime = query.StartTime;
        var endTime = query.EndTime;
        var entityId = query.EntityId;
        string? expectedEntityId = null;
        ValidateOptionalTimeRange(startTime, endTime, nameof(query));
        var path = "api/logbook";
        if (startTime.HasValue)
        {
            path += "/" + EscapeTimestamp(startTime.Value);
        }

        var parameters = new List<KeyValuePair<string, string?>>();
        AddTimestamp(parameters, "end_time", endTime);
        if (entityId is not null)
        {
            expectedEntityId = NormalizeEntityId(entityId, cancellationToken);
            parameters.Add(new KeyValuePair<string, string?>(
                "entity",
                expectedEntityId));
        }

        var rawEntries = await SendHomeAssistantAsync<JsonElement>(
            HttpMethod.Get,
            AppendQuery(path, parameters),
            null,
            cancellationToken).ConfigureAwait(false);
        if (rawEntries.ValueKind != JsonValueKind.Array
            || HomeAssistantJson.HasDuplicateProperties(rawEntries, cancellationToken))
        {
            throw new HomeAssistantProtocolException(
                "The Home Assistant logbook response contained duplicate properties or was not an array.");
        }
        var entries = HomeAssistantJson.DeserializeResponse<HomeAssistantLogbookEntry[]>(
            rawEntries,
            "The Home Assistant logbook response could not be decoded.",
            cancellationToken: cancellationToken);
        ValidateLogbookEntries(entries, startTime, endTime, expectedEntityId, cancellationToken);
        return entries;
    }

    internal static void ValidateLogbookEntries(
        IEnumerable<HomeAssistantLogbookEntry?> entries,
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        string? expectedEntityId,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry is null || !entry.When.HasValue)
            {
                throw new HomeAssistantProtocolException(
                    "The Home Assistant logbook response contained an entry without a timestamp.");
            }

            if (startTime.HasValue && entry.When.Value < startTime.Value
                || endTime.HasValue && entry.When.Value > endTime.Value)
            {
                throw new HomeAssistantProtocolException(
                    "The Home Assistant logbook response contained an entry outside the requested time range.");
            }

            if (expectedEntityId is not null
                && (!HomeAssistantEntityId.TryNormalize(entry.EntityId, out var returnedEntityId)
                    || !string.Equals(returnedEntityId, entry.EntityId, StringComparison.Ordinal)
                    || !string.Equals(returnedEntityId, expectedEntityId, StringComparison.Ordinal)))
            {
                throw new HomeAssistantProtocolException(
                    "The Home Assistant logbook response contained an entry for an unexpected entity.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>Gets the current Home Assistant error log as plaintext.</summary>
    public Task<string> GetErrorLogAsync(CancellationToken cancellationToken = default)
    {
        return SendTextAsync(HttpMethod.Get, "api/error_log", null, cancellationToken);
    }

    private static void ValidateOptionalTimeRange(
        DateTimeOffset? startTime,
        DateTimeOffset? endTime,
        string parameterName)
    {
        if (endTime.HasValue && !startTime.HasValue)
            throw new ArgumentException("An explicit end time requires an explicit start time.", parameterName);
        if (startTime.HasValue && endTime.HasValue && endTime.Value <= startTime.Value)
            throw new ArgumentOutOfRangeException(parameterName, "The end time must be after the start time.");
    }

    /// <summary>Gets an image from a camera entity.</summary>
    public Task<byte[]> GetCameraImageAsync(string entityId, CancellationToken cancellationToken = default)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "camera", cancellationToken, out var normalizedEntityId))
            throw new ArgumentException("A camera entity identifier is required.", nameof(entityId));
        return GetBytesAsync("api/camera_proxy/" + EscapePath(normalizedEntityId, cancellationToken), cancellationToken);
    }

    /// <summary>Gets all calendar entities.</summary>
    public async Task<IReadOnlyList<HomeAssistantCalendar>> GetCalendarsAsync(CancellationToken cancellationToken = default)
    {
        var rawCalendars = await SendHomeAssistantAsync<JsonElement>(HttpMethod.Get, "api/calendars", null, cancellationToken)
            .ConfigureAwait(false);
        if (rawCalendars.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantProtocolException(
                "The Home Assistant calendar list contained duplicate properties or was not an array.");
        }
        var hasDuplicateCalendarProperties = HomeAssistantJson.RunCancellationIsolated(
            () =>
            {
                foreach (var rawCalendar in rawCalendars.EnumerateArray())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (rawCalendar.ValueKind == JsonValueKind.Object
                        && HomeAssistantJson.HasDuplicateObjectPropertiesInline(rawCalendar, cancellationToken)) return true;
                }

                return false;
            },
            cancellationToken);
        if (hasDuplicateCalendarProperties)
            throw new HomeAssistantProtocolException(
                "The Home Assistant calendar list contained duplicate properties or was not an array.");
        foreach (var rawCalendar in rawCalendars.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (rawCalendar.ValueKind != JsonValueKind.Object)
            {
                throw new HomeAssistantProtocolException(
                    "The Home Assistant calendar list contained duplicate properties or was not an array.");
            }
        }
        var calendars = HomeAssistantJson.DeserializeResponse<HomeAssistantCalendar[]>(
            rawCalendars,
            "The Home Assistant calendar list could not be decoded.",
            cancellationToken: cancellationToken);
        HomeAssistantJson.RequireNoNullCollectionEntries(
            calendars,
            "The Home Assistant calendar list contained a null item.",
            cancellationToken: cancellationToken);
        var entityIds = new List<string>();
        foreach (var calendar in calendars)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantEntityId.TryNormalizeForDomain(calendar.EntityId, "calendar", cancellationToken, out var normalized)
                || !CancellationAwareString.EqualsOrdinal(calendar.EntityId, normalized, cancellationToken)
                || entityIds.Any(value => CancellationAwareString.EqualsOrdinal(value, normalized, cancellationToken)))
            {
                throw new HomeAssistantProtocolException("The Home Assistant calendar list contained an invalid or duplicate entity identifier.");
            }
            if (CancellationAwareString.IsNullOrWhiteSpace(calendar.Name, cancellationToken))
            {
                throw new HomeAssistantProtocolException("The Home Assistant calendar list contained an incomplete display name.");
            }
            entityIds.Add(normalized);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return calendars;
    }

    /// <summary>Gets calendar events in an exclusive time range.</summary>
    public async Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetCalendarEventsAsync(
        string entityId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "calendar", cancellationToken, out var normalizedEntityId))
        {
            throw new ArgumentException("A calendar entity identifier is required.", nameof(entityId));
        }

        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "The calendar end must be after its start.");
        }

        var path = AppendQuery(
            "api/calendars/" + EscapePath(normalizedEntityId, cancellationToken),
            new[]
            {
                new KeyValuePair<string, string?>("start", FormatTimestamp(start)),
                new KeyValuePair<string, string?>("end", FormatTimestamp(end))
            });
        var events = await SendHomeAssistantAsync<HomeAssistantCalendarEvent[]>(HttpMethod.Get, path, null, cancellationToken)
            .ConfigureAwait(false);
        HomeAssistantCalendarClient.ValidateEvents(events, cancellationToken);
        return events;
    }

    /// <summary>Creates or updates a state representation without controlling the underlying device.</summary>
    public async Task<HomeAssistantState> SetStateAsync(
        string entityId,
        HomeAssistantStateUpdate update,
        CancellationToken cancellationToken = default)
    {
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var normalizedEntityId = NormalizeEntityId(entityId, cancellationToken);
        var state = await SendHomeAssistantAsync<HomeAssistantState>(
            HttpMethod.Post,
            "api/states/" + EscapePath(normalizedEntityId, cancellationToken),
            update,
            cancellationToken).ConfigureAwait(false);
        return HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId, cancellationToken);
    }

    /// <summary>Deletes a state representation from Home Assistant.</summary>
    public Task<JsonElement> DeleteStateAsync(string entityId, CancellationToken cancellationToken = default)
    {
        return SendHomeAssistantAsync<JsonElement>(
            HttpMethod.Delete,
            "api/states/" + EscapePath(NormalizeEntityId(entityId, cancellationToken), cancellationToken),
            null,
            cancellationToken);
    }

    /// <summary>Fires an event through the REST event bus endpoint.</summary>
    public Task<JsonElement> FireEventAsync(
        string eventType,
        IReadOnlyDictionary<string, object?>? eventData = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CancellationAwareString.IsNullOrWhiteSpace(eventType, cancellationToken))
            throw new ArgumentException("An event type is required.", nameof(eventType));
        var normalizedEventType = CancellationAwareString.Trim(eventType, cancellationToken);
        var frozenEventData = HomeAssistantJson.FreezeObject(
            eventData ?? new Dictionary<string, object?>(), nameof(eventData), "EventData", cancellationToken);
        return SendHomeAssistantAsync<JsonElement>(
            HttpMethod.Post,
            "api/events/" + EscapePath(normalizedEventType, cancellationToken),
            frozenEventData,
            cancellationToken);
    }

    /// <summary>Renders a Home Assistant template and returns its text result.</summary>
    public Task<string> RenderTemplateAsync(
        string template,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            throw new ArgumentException("A template is required.", nameof(template));
        }

        var payload = new Dictionary<string, object?> { ["template"] = template };
        if (variables is not null)
        {
            payload["variables"] = HomeAssistantJson.FreezeObject(variables, nameof(variables), "Variables", cancellationToken);
        }

        return SendTextAsync(HttpMethod.Post, "api/template", payload, cancellationToken);
    }

    /// <summary>Checks whether the active Home Assistant configuration is valid.</summary>
    public Task<HomeAssistantConfigurationCheck> CheckConfigurationAsync(CancellationToken cancellationToken = default)
    {
        return SendHomeAssistantAsync<HomeAssistantConfigurationCheck>(
            HttpMethod.Post,
            "api/config/core/check_config",
            new Dictionary<string, object?>(),
            cancellationToken);
    }

    /// <summary>Invokes the Home Assistant intent API with an extensible request body.</summary>
    public Task<JsonElement> HandleIntentAsync(
        IReadOnlyDictionary<string, object?> request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        return SendHomeAssistantAsync<JsonElement>(HttpMethod.Post, "api/intent/handle", request, cancellationToken);
    }

    /// <summary>Processes text through the Home Assistant conversation API.</summary>
    public Task<JsonElement> ProcessConversationAsync(
        string text,
        string? language = null,
        string? agentId = null,
        string? conversationId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Conversation text is required.", nameof(text));
        }

        return SendHomeAssistantAsync<JsonElement>(
            HttpMethod.Post,
            "api/conversation/process",
            BuildConversationPayload(text, language, agentId, conversationId),
            cancellationToken);
    }

    private static void AddFlag(ICollection<KeyValuePair<string, string?>> parameters, string name, bool enabled)
    {
        if (enabled)
        {
            parameters.Add(new KeyValuePair<string, string?>(name, null));
        }
    }

    private static void AddTimestamp(
        ICollection<KeyValuePair<string, string?>> parameters,
        string name,
        DateTimeOffset? value)
    {
        if (value.HasValue)
        {
            parameters.Add(new KeyValuePair<string, string?>(name, FormatTimestamp(value.Value)));
        }
    }

    private static string AppendQuery(string path, IEnumerable<KeyValuePair<string, string?>> parameters)
    {
        var query = new StringBuilder();
        foreach (var parameter in parameters)
        {
            query.Append(query.Length == 0 ? '?' : '&');
            query.Append(Uri.EscapeDataString(parameter.Key));
            if (parameter.Value is not null)
            {
                query.Append('=');
                query.Append(Uri.EscapeDataString(parameter.Value));
            }
        }

        return path + query;
    }

    private static string EscapeTimestamp(DateTimeOffset value)
    {
        return Uri.EscapeDataString(FormatTimestamp(value));
    }

    private static string FormatTimestamp(DateTimeOffset value)
    {
        return value.ToString("o", CultureInfo.InvariantCulture);
    }

    private static IReadOnlyDictionary<string, object?> BuildConversationPayload(
        string text,
        string? language,
        string? agentId,
        string? conversationId)
    {
        var payload = new Dictionary<string, object?> { ["text"] = text };
        if (language is not null)
        {
            payload["language"] = RequireConversationSelector(language, nameof(language));
        }

        if (agentId is not null)
        {
            payload["agent_id"] = RequireConversationSelector(agentId, nameof(agentId));
        }

        if (conversationId is not null)
        {
            payload["conversation_id"] = RequireConversationSelector(conversationId, nameof(conversationId));
        }

        return payload;
    }

    private static string RequireConversationSelector(string value, string parameterName)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A supplied conversation selector cannot be empty.", parameterName)
            : value.Trim();
}
