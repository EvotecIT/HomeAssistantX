using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Services;
using HomeAssistantX.Subscriptions;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Notifications;

/// <summary>Reads and sends Home Assistant notifications and subscribes to persistent-notification updates.</summary>
public sealed class HomeAssistantNotificationClient
{
    private readonly HomeAssistantServiceClient _services;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantNotificationClient(HomeAssistantServiceClient services, HomeAssistantWebSocketClient webSocket)
    {
        _services = services;
        _webSocket = webSocket;
    }

    public async Task<IReadOnlyList<HomeAssistantPersistentNotification>> GetPersistentAsync(
        CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("persistent_notification/get", null, cancellationToken).ConfigureAwait(false);
        return value.Deserialize<HomeAssistantPersistentNotification[]>(HomeAssistantJson.SerializerOptions)
            ?? throw new HomeAssistantProtocolException("The Home Assistant persistent-notification response could not be decoded.");
    }

    public Task<HomeAssistantServiceCallResult> CreatePersistentAsync(
        string message,
        string? title = null,
        string? notificationId = null,
        CancellationToken cancellationToken = default)
    {
        var data = MessageData(message, title);
        AddOptional(data, "notification_id", notificationId);
        return _services.CallControlAsync(CreateCall("persistent_notification", "create", data), cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> DismissPersistentAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
        Require(notificationId, nameof(notificationId));
        return _services.CallControlAsync(
            new HomeAssistantServiceCall("persistent_notification", "dismiss").WithData("notification_id", notificationId),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> DismissAllPersistentAsync(CancellationToken cancellationToken = default)
        => _services.CallControlAsync(new HomeAssistantServiceCall("persistent_notification", "dismiss_all"), cancellationToken);

    /// <summary>Sends a message through current notify entities using the standard notify.send_message action.</summary>
    public Task<HomeAssistantServiceCallResult> SendAsync(
        HomeAssistantTarget target,
        string message,
        string? title = null,
        CancellationToken cancellationToken = default)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        var normalizedTarget = target.NormalizeForDomain("notify");
        if (!normalizedTarget.HasAnySelection())
        {
            throw new ArgumentException("At least one notification target is required.", nameof(target));
        }

        return _services.CallControlAsync(
            CreateCall("notify", "send_message", MessageData(message, title)).ForTarget(normalizedTarget),
            cancellationToken);
    }

    public Task<IHomeAssistantSubscription> SubscribePersistentAsync(
        Func<HomeAssistantPersistentNotificationUpdate, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return _webSocket.SubscribeAsync("persistent_notification/subscribe", null, async (value, token) =>
        {
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty("type", out var typeValue)
                || typeValue.ValueKind != JsonValueKind.String
                || !value.TryGetProperty("notifications", out var notificationsValue)
                || notificationsValue.ValueKind != JsonValueKind.Object)
            {
                throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update had an unexpected shape.");
            }

            var rawType = typeValue.GetString() ?? string.Empty;
            var notifications = notificationsValue.Deserialize<Dictionary<string, HomeAssistantPersistentNotification>>(HomeAssistantJson.SerializerOptions)
                ?? throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update could not be decoded.");
            await handler(new HomeAssistantPersistentNotificationUpdate
            {
                RawType = rawType,
                Type = ParseType(rawType),
                Notifications = new Dictionary<string, HomeAssistantPersistentNotification>(notifications, StringComparer.Ordinal),
                Raw = value.Clone()
            }, token).ConfigureAwait(false);
        }, cancellationToken);
    }

    private static Dictionary<string, object?> MessageData(string message, string? title)
    {
        Require(message, nameof(message));
        var data = new Dictionary<string, object?> { ["message"] = message };
        AddOptional(data, "title", title);
        return data;
    }

    private static HomeAssistantServiceCall CreateCall(
        string domain,
        string service,
        IReadOnlyDictionary<string, object?> data)
    {
        var call = new HomeAssistantServiceCall(domain, service);
        foreach (var pair in data)
        {
            call.WithData(pair.Key, pair.Value);
        }

        return call;
    }

    private static void AddOptional(IDictionary<string, object?> data, string name, string? value)
    {
        if (value is not null)
        {
            data[name] = value;
        }
    }

    private static void Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }

    private static HomeAssistantPersistentNotificationUpdateType ParseType(string value)
    {
        if (string.Equals(value, "current", StringComparison.OrdinalIgnoreCase))
            return HomeAssistantPersistentNotificationUpdateType.Current;
        if (string.Equals(value, "added", StringComparison.OrdinalIgnoreCase))
            return HomeAssistantPersistentNotificationUpdateType.Added;
        if (string.Equals(value, "updated", StringComparison.OrdinalIgnoreCase))
            return HomeAssistantPersistentNotificationUpdateType.Updated;
        if (string.Equals(value, "removed", StringComparison.OrdinalIgnoreCase))
            return HomeAssistantPersistentNotificationUpdateType.Removed;
        return HomeAssistantPersistentNotificationUpdateType.Unknown;
    }
}
