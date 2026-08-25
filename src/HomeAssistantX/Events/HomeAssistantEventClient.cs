using System.Text.Json;
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
                var value = message.Deserialize<HomeAssistantEvent>(HomeAssistantJson.SerializerOptions)
                    ?? throw new Exceptions.HomeAssistantProtocolException("A Home Assistant event could not be decoded.");
                await handler(value, token).ConfigureAwait(false);
            },
            cancellationToken);
    }
}
