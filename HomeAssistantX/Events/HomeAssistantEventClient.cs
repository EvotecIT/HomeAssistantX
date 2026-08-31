using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Subscriptions;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Events;

/// <summary>Subscribes to the Home Assistant event bus without polling.</summary>
public sealed class HomeAssistantEventClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantEventClient(HomeAssistantWebSocketClient webSocket)
    {
        _webSocket = webSocket;
    }

    public Task<IHomeAssistantSubscription> SubscribeAsync(
        string? eventType,
        Func<HomeAssistantEvent, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        IReadOnlyDictionary<string, object?>? payload = string.IsNullOrWhiteSpace(eventType)
            ? null
            : new Dictionary<string, object?> { ["event_type"] = eventType };
        return _webSocket.SubscribeAsync(
            "subscribe_events",
            payload,
            async (message, token) =>
            {
                var value = HomeAssistantSubscriptionProjectionException.Capture(() =>
                    ValidateEvent(
                        HomeAssistantJson.DeserializeResponse<HomeAssistantEvent>(
                            message,
                            "A Home Assistant event could not be decoded.",
                            cancellationToken: token),
                        token));
                await handler(value, token).ConfigureAwait(false);
            },
            cancellationToken);
    }

    /// <summary>Fires an event through the WebSocket API.</summary>
    public Task<JsonElement> FireAsync(
        string eventType,
        IReadOnlyDictionary<string, object?>? eventData = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("An event type is required.", nameof(eventType));
        }

        var payload = new Dictionary<string, object?> { ["event_type"] = eventType };
        if (eventData is not null)
        {
            payload["event_data"] = eventData;
        }

        return _webSocket.RequestAsync("fire_event", payload, cancellationToken);
    }

    /// <summary>Subscribes to a Home Assistant trigger definition without polling.</summary>
    public Task<IHomeAssistantSubscription> SubscribeTriggerAsync(
        object trigger,
        Func<JsonElement, CancellationToken, Task> handler,
        IReadOnlyDictionary<string, object?>? variables = null,
        CancellationToken cancellationToken = default)
    {
        if (trigger is null)
        {
            throw new ArgumentNullException(nameof(trigger));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var payload = new Dictionary<string, object?> { ["trigger"] = trigger };
        if (variables is not null)
        {
            payload["variables"] = variables;
        }

        return _webSocket.SubscribeAsync("subscribe_trigger", payload, handler, cancellationToken);
    }
    private static HomeAssistantEvent ValidateEvent(
        HomeAssistantEvent value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(value.EventType)
            || value.Data is null)
        {
            throw new HomeAssistantProtocolException(
                "A Home Assistant event omitted its required event type or data object.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }
}
