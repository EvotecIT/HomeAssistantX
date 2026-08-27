using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.Json;
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
        var path = "api/logbook";
        if (query.StartTime.HasValue)
        {
            path += "/" + EscapeTimestamp(query.StartTime.Value);
        }

        var parameters = new List<KeyValuePair<string, string?>>();
        AddTimestamp(parameters, "end_time", query.EndTime);
        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            parameters.Add(new KeyValuePair<string, string?>("entity", NormalizeEntityId(query.EntityId!, cancellationToken)));
        }

        return await SendHomeAssistantAsync<HomeAssistantLogbookEntry[]>(
            HttpMethod.Get,
            AppendQuery(path, parameters),
            null,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Gets the current Home Assistant error log as plaintext.</summary>
    public Task<string> GetErrorLogAsync(CancellationToken cancellationToken = default)
    {
        return SendTextAsync(HttpMethod.Get, "api/error_log", null, cancellationToken);
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
        return await SendHomeAssistantAsync<HomeAssistantCalendar[]>(HttpMethod.Get, "api/calendars", null, cancellationToken)
            .ConfigureAwait(false);
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
        return await SendHomeAssistantAsync<HomeAssistantCalendarEvent[]>(HttpMethod.Get, path, null, cancellationToken)
            .ConfigureAwait(false);
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
        var frozenEventData = HomeAssistantJson.FreezeObject(
            eventData ?? new Dictionary<string, object?>(), nameof(eventData), "EventData", cancellationToken);
        return SendHomeAssistantAsync<JsonElement>(
            HttpMethod.Post,
            "api/events/" + EscapePath(eventType, cancellationToken),
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

        var payload = new Dictionary<string, object?> { ["template"] = template.Trim() };
        if (variables is not null)
        {
            payload["variables"] = HomeAssistantJson.FreezeObject(variables, nameof(variables), "Variables");
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
