using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;
using HomeAssistantX.Subscriptions;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Calendars;

/// <summary>Discovers calendars, manages events, and subscribes to event-list changes without polling.</summary>
public sealed class HomeAssistantCalendarClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantCalendarClient(HomeAssistantRestClient rest, HomeAssistantWebSocketClient webSocket)
    {
        _rest = rest;
        _webSocket = webSocket;
    }

    public async Task<IReadOnlyList<HomeAssistantCalendar>> GetAsync(CancellationToken cancellationToken = default)
    {
        var calendars = await _rest.GetCalendarsAsync(cancellationToken).ConfigureAwait(false);
        HomeAssistantJson.RequireNoNullCollectionEntries(
            calendars,
            "The Home Assistant calendar list contained a null item.",
            cancellationToken: cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var entityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var calendar in calendars)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantEntityId.TryNormalizeForDomain(calendar.EntityId, "calendar", out var normalized)
                || !string.Equals(calendar.EntityId, normalized, StringComparison.Ordinal)
                || !entityIds.Add(normalized))
            {
                throw new HomeAssistantProtocolException("The Home Assistant calendar list contained an invalid or duplicate entity identifier.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        return calendars;
    }

    public async Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetEventsAsync(
        string entityId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        ValidateRange(start, end);
        var events = await _rest.GetCalendarEventsAsync(normalizedEntityId, start, end, cancellationToken).ConfigureAwait(false);
        ValidateEvents(events, cancellationToken);
        return events;
    }

    public async Task CreateEventAsync(
        string entityId,
        HomeAssistantCalendarEventInput eventInput,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        if (eventInput is null)
        {
            throw new ArgumentNullException(nameof(eventInput));
        }

        await _webSocket.RequestAsync("calendar/event/create", new Dictionary<string, object?>
        {
            ["entity_id"] = normalizedEntityId,
            ["event"] = eventInput.ToPayload()
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateEventAsync(
        string entityId,
        HomeAssistantCalendarEventReference eventReference,
        HomeAssistantCalendarEventInput eventInput,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        if (eventReference is null)
        {
            throw new ArgumentNullException(nameof(eventReference));
        }

        if (eventInput is null)
        {
            throw new ArgumentNullException(nameof(eventInput));
        }

        var payload = new Dictionary<string, object?>
        {
            ["entity_id"] = normalizedEntityId,
            ["event"] = eventInput.ToPayload()
        };
        eventReference.AddTo(payload);
        await _webSocket.RequestAsync("calendar/event/update", payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteEventAsync(
        string entityId,
        HomeAssistantCalendarEventReference eventReference,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        if (eventReference is null)
        {
            throw new ArgumentNullException(nameof(eventReference));
        }

        var payload = new Dictionary<string, object?> { ["entity_id"] = normalizedEntityId };
        eventReference.AddTo(payload);
        await _webSocket.RequestAsync("calendar/event/delete", payload, cancellationToken).ConfigureAwait(false);
    }

    public Task<IHomeAssistantSubscription> SubscribeAsync(
        string entityId,
        DateTimeOffset start,
        DateTimeOffset end,
        Func<HomeAssistantCalendarEventUpdate, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        ValidateRange(start, end);
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var payload = new Dictionary<string, object?>
        {
            ["entity_id"] = normalizedEntityId,
            ["start"] = start.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            ["end"] = end.ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        };
        return _webSocket.SubscribeAsync("calendar/event/subscribe", payload, async (value, token) =>
        {
            token.ThrowIfCancellationRequested();
            if (value.ValueKind != JsonValueKind.Null && value.ValueKind != JsonValueKind.Array)
            {
                throw new HomeAssistantProtocolException("The Home Assistant calendar subscription had an unexpected shape.");
            }

            var update = new HomeAssistantCalendarEventUpdate
            {
                IsAvailable = value.ValueKind == JsonValueKind.Array,
                // WebSocket event payloads are already detached before routing.
                Raw = value
            };
            if (update.IsAvailable)
            {
                update.Events = HomeAssistantJson.DeserializeResponse<HomeAssistantCalendarEvent[]>(
                    value,
                    "The Home Assistant calendar subscription could not be decoded.",
                    cancellationToken: token);
                ValidateEvents(update.Events, token);
            }

            token.ThrowIfCancellationRequested();
            await handler(update, token).ConfigureAwait(false);
        }, cancellationToken);
    }

    private static void ValidateRange(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "The calendar range end must be after its start.");
        }
    }

    internal static void ValidateEvents(
        IReadOnlyList<HomeAssistantCalendarEvent> events,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        foreach (var item in events)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(item.Summary) || item.Start is null || item.End is null)
            {
                throw new HomeAssistantProtocolException("Home Assistant returned an incomplete calendar event.");
            }

            var allDay = item.Start.Date is not null && item.End.Date is not null;
            var timed = item.Start.DateTime.HasValue && item.End.DateTime.HasValue;
            if ((!allDay && !timed)
                || (allDay && string.CompareOrdinal(item.End.Date, item.Start.Date) <= 0)
                || (timed && item.End.DateTime <= item.Start.DateTime))
            {
                throw new HomeAssistantProtocolException("Home Assistant returned a calendar event with an invalid range.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static string NormalizeEntityId(string entityId)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "calendar", out var normalized))
        {
            throw new ArgumentException("A calendar entity identifier is required.", nameof(entityId));
        }

        return normalized;
    }
}
