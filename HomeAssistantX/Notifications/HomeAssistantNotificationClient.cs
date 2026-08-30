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
        ValidateNotificationObjects(value, dictionary: false, "The Home Assistant persistent-notification response", cancellationToken);
        var notifications = HomeAssistantJson.DeserializeResponse<HomeAssistantPersistentNotification[]>(
            value,
            "The Home Assistant persistent-notification response could not be decoded.",
            cancellationToken: cancellationToken);
        ValidateNotifications(notifications, "The Home Assistant persistent-notification response", cancellationToken);
        return notifications;
    }

    public Task<HomeAssistantServiceCallResult> CreatePersistentAsync(
        string message,
        string? title = null,
        string? notificationId = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (notificationId is not null)
        {
            Require(notificationId, nameof(notificationId), cancellationToken);
        }

        var data = MessageData(message, title, cancellationToken);
        AddOptional(data, "notification_id", notificationId);
        return _services.CallControlAsync(CreateCall("persistent_notification", "create", data), cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> DismissPersistentAsync(
        string notificationId,
        CancellationToken cancellationToken = default)
    {
        Require(notificationId, nameof(notificationId), cancellationToken);
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

        var normalizedTarget = target.NormalizeForDomain("notify", cancellationToken);
        if (!normalizedTarget.HasAnySelection())
        {
            throw new ArgumentException("At least one notification target is required.", nameof(target));
        }

        return _services.CallControlAsync(
            CreateCall("notify", "send_message", MessageData(message, title, cancellationToken)).ForTarget(normalizedTarget),
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
            HomeAssistantPersistentNotificationUpdate update;
            try
            {
                update = await ProjectPersistentUpdateAsync(value, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new HomeAssistantSubscriptionProjectionException(ex);
            }

            await handler(update, token).ConfigureAwait(false);
        }, cancellationToken);
    }

    private static async Task<HomeAssistantPersistentNotificationUpdate> ProjectPersistentUpdateAsync(
        JsonElement value,
        CancellationToken token)
    {
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update had an unexpected shape.");
            }

            var hasType = false;
            var hasNotifications = false;
            var typeValue = default(JsonElement);
            var notificationsValue = default(JsonElement);
            foreach (var property in value.EnumerateObject())
            {
                token.ThrowIfCancellationRequested();
                if (CancellationAwareString.EqualsOrdinal(property.Name, "type", token))
                {
                    if (hasType)
                        throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update contained a duplicate type field.");
                    hasType = true;
                    typeValue = property.Value;
                }
                else if (CancellationAwareString.EqualsOrdinal(property.Name, "notifications", token))
                {
                    if (hasNotifications)
                        throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update contained a duplicate notifications field.");
                    hasNotifications = true;
                    notificationsValue = property.Value;
                }
            }

            if (!hasType
                || typeValue.ValueKind != JsonValueKind.String
                || !hasNotifications
                || notificationsValue.ValueKind != JsonValueKind.Object)
            {
                throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update had an unexpected shape.");
            }

            var rawType = typeValue.GetString() ?? string.Empty;
            if (CancellationAwareString.IsNullOrWhiteSpace(rawType, token)
                || !CancellationAwareString.EqualsOrdinal(
                    rawType,
                    CancellationAwareString.Trim(rawType, token),
                    token))
            {
                throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update contained an invalid event type.");
            }
            ValidateNotificationObjects(
                notificationsValue,
                dictionary: true,
                "The Home Assistant persistent-notification update",
                token);
            var notifications = DeserializeNotificationDictionary(
                notificationsValue,
                "The Home Assistant persistent-notification update could not be decoded.",
                token);
            ValidateNotifications(notifications.Values, "The Home Assistant persistent-notification update", token);
            foreach (var item in notifications)
            {
                token.ThrowIfCancellationRequested();
                if (!CancellationAwareString.EqualsOrdinal(item.Key, item.Value.NotificationId, token))
                {
                    throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update contained a mismatched notification identifier.");
                }
            }
            var raw = await HomeAssistantJson.SnapshotResponseAsync(
                value,
                "The Home Assistant persistent-notification update could not be snapshotted.",
                token).ConfigureAwait(false);
            return new HomeAssistantPersistentNotificationUpdate
            {
                RawType = rawType,
                Type = ParseType(rawType, token),
                Notifications = notifications,
                Raw = raw
            };
    }

    internal static void ValidateNotifications(
        IEnumerable<HomeAssistantPersistentNotification> notifications,
        string responseName,
        CancellationToken cancellationToken)
    {
        var identifiers = new List<string>();
        foreach (var item in notifications)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item is null)
                throw new HomeAssistantProtocolException(responseName + " contained a null item.");
            if (CancellationAwareString.IsNullOrWhiteSpace(item.NotificationId, cancellationToken)
                || CancellationAwareString.IsNullOrWhiteSpace(item.Message, cancellationToken))
                throw new HomeAssistantProtocolException(responseName + " contained an incomplete item.");
            if (identifiers.Any(value => CancellationAwareString.EqualsOrdinal(value, item.NotificationId, cancellationToken)))
                throw new HomeAssistantProtocolException(responseName + " contained a duplicate notification identifier.");
            identifiers.Add(item.NotificationId);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static Dictionary<string, HomeAssistantPersistentNotification> DeserializeNotificationDictionary(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.Object)
            throw new HomeAssistantProtocolException(failureMessage);

        var result = new Dictionary<string, HomeAssistantPersistentNotification>(
            new CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (result.ContainsKey(property.Name))
                throw new HomeAssistantProtocolException("The Home Assistant persistent-notification update contained a duplicate notification identifier.");
            result.Add(
                property.Name,
                HomeAssistantJson.DeserializeResponse<HomeAssistantPersistentNotification>(
                    property.Value,
                    failureMessage,
                    cancellationToken: cancellationToken));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static void ValidateNotificationObjects(
        JsonElement value,
        bool dictionary,
        string responseName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (dictionary ? value.ValueKind != JsonValueKind.Object : value.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantProtocolException(responseName + " had an unexpected shape.");
        }

        var objects = dictionary
            ? value.EnumerateObject().Select(property => property.Value)
            : value.EnumerateArray();
        foreach (var item in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.Object) continue;
            var names = new HashSet<string>(
                new CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
            var knownFields = 0;
            foreach (var property in item.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!names.Add(property.Name))
                    throw new HomeAssistantProtocolException(responseName + " contained a duplicate notification field.");

                var field = NotificationField(property.Name, cancellationToken);
                if (field == 0) continue;
                if ((knownFields & field) != 0)
                    throw new HomeAssistantProtocolException(responseName + " contained a duplicate notification field.");
                knownFields |= field;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static int NotificationField(string name, CancellationToken cancellationToken)
    {
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(name, "notification_id", cancellationToken)) return 1;
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(name, "message", cancellationToken)) return 2;
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(name, "title", cancellationToken)) return 4;
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(name, "created_at", cancellationToken)) return 8;
        return 0;
    }

    private static Dictionary<string, object?> MessageData(
        string message,
        string? title,
        CancellationToken cancellationToken)
    {
        Require(message, nameof(message), cancellationToken);
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

    private static void Require(
        string value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }

    private static HomeAssistantPersistentNotificationUpdateType ParseType(
        string value,
        CancellationToken cancellationToken)
    {
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(value, "current", cancellationToken))
            return HomeAssistantPersistentNotificationUpdateType.Current;
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(value, "added", cancellationToken))
            return HomeAssistantPersistentNotificationUpdateType.Added;
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(value, "updated", cancellationToken))
            return HomeAssistantPersistentNotificationUpdateType.Updated;
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(value, "removed", cancellationToken))
            return HomeAssistantPersistentNotificationUpdateType.Removed;
        return HomeAssistantPersistentNotificationUpdateType.Unknown;
    }
}
