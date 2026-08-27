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

    public Task<IReadOnlyList<HomeAssistantCalendar>> GetAsync(CancellationToken cancellationToken = default)
        => _rest.GetCalendarsAsync(cancellationToken);

    public Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetEventsAsync(
        string entityId,
        DateTimeOffset start,
        DateTimeOffset end,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        ValidateRange(start, end);
        return _rest.GetCalendarEventsAsync(normalizedEntityId, start, end, cancellationToken);
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
            var update = new HomeAssistantCalendarEventUpdate
            {
                IsAvailable = value.ValueKind == JsonValueKind.Array,
                Raw = value.Clone()
            };
            if (update.IsAvailable)
            {
                update.Events = HomeAssistantJson.DeserializeResponse<HomeAssistantCalendarEvent[]>(
                    value,
                    "The Home Assistant calendar subscription could not be decoded.");
            }

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

    private static string NormalizeEntityId(string entityId)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "calendar", out var normalized))
        {
            throw new ArgumentException("A calendar entity identifier is required.", nameof(entityId));
        }

        return normalized;
    }
}
