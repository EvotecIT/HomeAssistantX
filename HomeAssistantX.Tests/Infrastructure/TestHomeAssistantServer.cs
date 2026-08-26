using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HomeAssistantX.Tests.Infrastructure;

internal sealed partial class TestHomeAssistantServer : IDisposable
{
    private const string WebSocketMagic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _source = new();
    private readonly ConcurrentDictionary<int, SocketSession> _sessions = new();
    private readonly ConcurrentDictionary<string, string> _lastWebSocketCommands = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _serviceCallBodies = new();
    private readonly Task _acceptTask;
    private readonly object _stateGate = new();
    private string _statesJson = DefaultStatesJson;
#if !NET472
    private int _nextSessionId;
#endif
    private int _connectionCount;
    private int _eventSequence;
    private int _failNextSubscription;
    private TaskCompletionSource<bool>? _pausedSubscriptionReceived;
    private TaskCompletionSource<bool>? _pausedSubscriptionRelease;
    private TaskCompletionSource<bool>? _pausedServiceCallReceived;
    private TaskCompletionSource<bool>? _pausedServiceCallRelease;
    private TaskCompletionSource<bool>? _pausedGetStatesReceived;
    private TaskCompletionSource<bool>? _pausedGetStatesRelease;
    private readonly TaskCompletionSource<bool> _unsubscribeReceived =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> _systemHealthEventsSent =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _unsubscribeCommandCount;
    private int _invalidUnsubscribeCommandCount;
    private int _lastSubscriptionSessionId;
    private int _lastUnsubscribeSessionId;
    private int _authenticatedRequestCount;
    private int _unauthorizedRequestCount;
    private string _requiredAccessToken = AccessToken;
    private int _disposed;

    public TestHomeAssistantServer()
    {
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        var endpoint = (IPEndPoint)_listener.LocalEndpoint;
        BaseUri = new Uri("http://127.0.0.1:" + endpoint.Port + "/", UriKind.Absolute);
        _acceptTask = Task.Run(AcceptLoopAsync);
    }

    public const string AccessToken = "test-access-token";

    public Uri BaseUri { get; }

    public int WebSocketConnectionCount => Volatile.Read(ref _connectionCount);

    public int AuthenticatedRequestCount => Volatile.Read(ref _authenticatedRequestCount);

    public int UnauthorizedRequestCount => Volatile.Read(ref _unauthorizedRequestCount);

    public string RequiredAccessToken
    {
        get => Volatile.Read(ref _requiredAccessToken);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A required access token is required.", nameof(value));
            }

            Volatile.Write(ref _requiredAccessToken, value);
        }
    }

    public string? LastAuthorization { get; private set; }

    public string? LastServiceCallBody { get; private set; }

    public IReadOnlyList<string> ServiceCallBodies => _serviceCallBodies.ToArray();

    public (Task Received, Action Release) PauseNextServiceCall()
    {
        lock (_stateGate)
        {
            if (_pausedServiceCallReceived is not null)
                throw new InvalidOperationException("A service call is already paused.");
            var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pausedServiceCallReceived = received;
            _pausedServiceCallRelease = release;
            return (received.Task, () => release.TrySetResult(true));
        }
    }

    public (Task Received, Action Release) PauseNextGetStates()
    {
        lock (_stateGate)
        {
            if (_pausedGetStatesReceived is not null)
                throw new InvalidOperationException("A get_states request is already paused.");
            var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pausedGetStatesReceived = received;
            _pausedGetStatesRelease = release;
            return (received.Task, () => release.TrySetResult(true));
        }
    }

    public void ClearLastServiceCall()
    {
        LastServiceCallBody = null;
        while (_serviceCallBodies.TryDequeue(out _))
        {
        }
    }

    public string? LastRequestBody { get; private set; }

    public string? LastRequestPath { get; private set; }

    public string? ExactStateResponseJson { get; set; }
    public string StateMutationResponseJson { get; set; } =
        "{\"entity_id\":\"sensor.virtual\",\"state\":\"ready\",\"attributes\":{\"friendly_name\":\"Virtual\"}}";

    public int OAuthTokenRequestCount { get; private set; }

    public string? LastRevokedRefreshToken { get; private set; }

    public int UnsubscribeCommandCount => Volatile.Read(ref _unsubscribeCommandCount);

    public int InvalidUnsubscribeCommandCount => Volatile.Read(ref _invalidUnsubscribeCommandCount);

    public int LastSubscriptionSessionId => Volatile.Read(ref _lastSubscriptionSessionId);

    public int LastUnsubscribeSessionId => Volatile.Read(ref _lastUnsubscribeSessionId);

    public string? GetLastWebSocketCommand(string commandType)
    {
        return _lastWebSocketCommands.TryGetValue(commandType, out var command) ? command : null;
    }

    public void ClearLastWebSocketCommand(string commandType)
    {
        _lastWebSocketCommands.TryRemove(commandType, out _);
    }

    public bool SendStateChangeBeforeSnapshot { get; set; }

    public string? ConfigEntriesErrorCode { get; set; }

    public bool OmitSystemHealthFinish { get; set; }
    public string SystemHealthInitialEventJson { get; set; } =
        "{\"type\":\"initial\",\"data\":{\"homeassistant\":{\"info\":{\"version\":\"2026.8.3\",\"installation_type\":\"Home Assistant OS\",\"hassio\":true}}}}";

    public bool IgnoreUnsubscribeAcknowledgement { get; set; }

    public bool RejectSupportedFeatures { get; set; }

    public string? SupportedFeaturesErrorCode { get; set; }

    public string SupportedFeaturesErrorMessage { get; set; } = "Feature negotiation was rejected.";

    public bool OmitSupportedFeaturesErrorMessage { get; set; }

    public bool ReturnMalformedSupportedFeatures { get; set; }

    public bool CoalesceSupportedFeaturesAcknowledgement { get; set; }

    public string? SupportedFeaturesSuccessJson { get; set; }
    public bool OmitSupportedFeaturesResult { get; set; }

    public bool ReturnInvalidUpdateReleaseNotes { get; set; }

    public string HistoryResponseJson { get; set; } = "[[" + KitchenTemperatureStateJson + "]]";

    public string ActionCatalogResponseJson { get; set; } =
        "{\"light\":{\"turn_on\":{\"name\":\"Turn on\",\"description\":\"Turns on a light.\",\"fields\":{\"brightness_pct\":{\"name\":\"Brightness\",\"description\":\"Brightness percentage.\",\"required\":false,\"example\":45,\"selector\":{\"number\":{\"min\":0,\"max\":100}}}},\"target\":{\"entity\":[{\"domain\":\"light\"}]}},\"turn_off\":{\"name\":\"Turn off\"},\"toggle\":{\"name\":\"Toggle\"}},\"switch\":{\"turn_on\":{\"name\":\"Turn on\"},\"turn_off\":{\"name\":\"Turn off\"},\"toggle\":{\"name\":\"Toggle\"}}}";

    public string ConfigurationResponseJson { get; set; } =
        "{\"location_name\":\"Test Home\",\"Location_Name\":\"Case-distinct extension\",\"time_zone\":\"Europe/Warsaw\",\"version\":\"2026.8.3\",\"state\":\"RUNNING\",\"components\":[\"api\",\"websocket_api\"],\"custom_field\":42}";

    public string? ExtendedEntityRegistryResponseJson { get; set; }

    public bool PublishNullStateEventData { get; set; }

    public string? SignedPathResponseJson { get; set; }

    public Task WaitForSystemHealthEventsAsync()
    {
        return _systemHealthEventsSent.Task;
    }

    public void SetStates(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new ArgumentException("State fixture must be a JSON array.", nameof(json));
        }

        lock (_stateGate)
        {
            _statesJson = json;
        }
    }

    public void FailNextSubscription()
    {
        Interlocked.Exchange(ref _failNextSubscription, 1);
    }

    public void PauseNextSubscription()
    {
        _pausedSubscriptionReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pausedSubscriptionRelease = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public Task WaitForPausedSubscriptionAsync()
    {
        return (_pausedSubscriptionReceived
            ?? throw new InvalidOperationException("No subscription pause is configured.")).Task;
    }

    public void ReleasePausedSubscription()
    {
        (_pausedSubscriptionRelease
            ?? throw new InvalidOperationException("No subscription pause is configured.")).TrySetResult(true);
    }

    public Task WaitForUnsubscribeAsync()
    {
        return _unsubscribeReceived.Task;
    }

    public async Task<int> PublishStateChangeAsync(
        string entityId,
        string? oldStateJson,
        string? newStateJson,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object?>
        {
            ["entity_id"] = entityId,
            ["old_state"] = ParseOptional(oldStateJson),
            ["new_state"] = ParseOptional(newStateJson)
        };
        var recipients = 0;
        foreach (var session in _sessions.Values)
        {
            if (session.StateSubscriptionId is int subscriptionId)
            {
                recipients++;
                await session.SendAsync(new Dictionary<string, object?>
                {
                    ["id"] = subscriptionId,
                    ["type"] = "event",
                    ["event"] = new Dictionary<string, object?>
                    {
                        ["event_type"] = "state_changed",
                        ["data"] = PublishNullStateEventData ? null : data,
                        ["origin"] = "LOCAL",
                        ["time_fired"] = DateTimeOffset.UtcNow,
                        ["context"] = new Dictionary<string, object?>
                        {
                            ["id"] = "ctx-" + Interlocked.Increment(ref _eventSequence),
                            ["trace_hint"] = "test-trace"
                        },
                        ["custom_event_field"] = "preserved"
                    }
                }, cancellationToken).ConfigureAwait(false);
            }
        }

        return recipients;
    }

    public async Task<int> PublishRawStateEventAsync(
        string eventJson,
        CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(eventJson);
        var payload = document.RootElement.Clone();
        var recipients = 0;
        foreach (var session in _sessions.Values)
        {
            if (session.StateSubscriptionId is not int subscriptionId) continue;
            recipients++;
            await session.SendSubscriptionEventAsync(
                subscriptionId,
                payload,
                cancellationToken).ConfigureAwait(false);
        }

        return recipients;
    }

    public async Task DropWebSocketsAsync()
    {
        foreach (var session in _sessions.Values)
        {
            await session.AbortAsync().ConfigureAwait(false);
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_source.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException) when (_source.IsCancellationRequested)
            {
                return;
            }
            catch (InvalidOperationException) when (_source.IsCancellationRequested)
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true);
            var requestLine = await reader.ReadLineAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(requestLine))
            {
                return;
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrEmpty(line))
                {
                    break;
                }

                var separator = line.IndexOf(':');
                if (separator > 0)
                {
                    headers[line.Substring(0, separator).Trim()] = line.Substring(separator + 1).Trim();
                }
            }

            if (headers.TryGetValue("Upgrade", out var upgrade)
                && string.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase))
            {
                await HandleWebSocketAsync(client, stream, headers).ConfigureAwait(false);
                return;
            }

            var body = string.Empty;
            if (headers.TryGetValue("Content-Length", out var contentLengthValue)
                && int.TryParse(contentLengthValue, out var contentLength)
                && contentLength > 0)
            {
                var chars = new char[contentLength];
                var read = 0;
                while (read < chars.Length)
                {
                    var count = await reader.ReadAsync(chars, read, chars.Length - read).ConfigureAwait(false);
                    if (count == 0)
                    {
                        break;
                    }

                    read += count;
                }

                body = new string(chars, 0, read);
            }

            await HandleHttpAsync(stream, requestLine, headers, body).ConfigureAwait(false);
        }
    }

    private async Task HandleWebSocketAsync(
        TcpClient client,
        NetworkStream stream,
        IReadOnlyDictionary<string, string> headers)
    {
#if NET472
        await Task.Yield();
        throw new PlatformNotSupportedException("The loopback WebSocket test server runs on modern .NET.");
#else
        if (!headers.TryGetValue("Sec-WebSocket-Key", out var key))
        {
            return;
        }

        var accept = ComputeWebSocketAccept(key);
        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 101 Switching Protocols\r\n"
            + "Upgrade: websocket\r\n"
            + "Connection: Upgrade\r\n"
            + "Sec-WebSocket-Accept: " + accept + "\r\n\r\n");
        await stream.WriteAsync(response, 0, response.Length, _source.Token).ConfigureAwait(false);
        await stream.FlushAsync(_source.Token).ConfigureAwait(false);

        using var socket = WebSocket.CreateFromStream(stream, true, null, TimeSpan.FromSeconds(30));
        var sessionId = Interlocked.Increment(ref _nextSessionId);
        var session = new SocketSession(socket);
        _sessions[sessionId] = session;
        Interlocked.Increment(ref _connectionCount);
        try
        {
            await session.SendAsync(new Dictionary<string, object?>
            {
                ["type"] = "auth_required",
                ["ha_version"] = "2026.8.3"
            }, _source.Token).ConfigureAwait(false);

            var authentication = await session.ReceiveAsync(_source.Token).ConfigureAwait(false);
            using (var authDocument = JsonDocument.Parse(authentication))
            {
                var root = authDocument.RootElement;
                var valid = root.TryGetProperty("type", out var type)
                    && type.GetString() == "auth"
                    && root.TryGetProperty("access_token", out var token)
                    && token.GetString() == RequiredAccessToken;
                if (!valid)
                {
                    await session.SendAsync(new Dictionary<string, object?>
                    {
                        ["type"] = "auth_invalid",
                        ["message"] = "Invalid access token"
                    }, _source.Token).ConfigureAwait(false);
                    return;
                }
            }

            await session.SendAsync(new Dictionary<string, object?>
            {
                ["type"] = "auth_ok",
                ["ha_version"] = "2026.8.3"
            }, _source.Token).ConfigureAwait(false);

            while (!_source.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var commandJson = await session.ReceiveAsync(_source.Token).ConfigureAwait(false);
                using var commandDocument = JsonDocument.Parse(commandJson);
                var command = commandDocument.RootElement;
                var id = command.GetProperty("id").GetInt32();
                var type = command.GetProperty("type").GetString() ?? string.Empty;
                await HandleWebSocketCommandAsync(sessionId, session, id, type, command).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_source.IsCancellationRequested)
        {
        }
        catch (WebSocketException)
        {
        }
        catch (IOException)
        {
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            client.Dispose();
        }
#endif
    }

    private async Task HandleWebSocketCommandAsync(
        int sessionId,
        SocketSession session,
        int id,
        string type,
        JsonElement command)
    {
        _lastWebSocketCommands[type] = command.GetRawText();
        switch (type)
        {
            case "supported_features":
                if (ReturnMalformedSupportedFeatures)
                {
                    await session.SendTextAsync("{not-json", _source.Token).ConfigureAwait(false);
                    return;
                }

                if (RejectSupportedFeatures)
                {
                    await session.SendErrorAsync(id, "unknown_command", "Unknown command.", "unknown_command", _source.Token).ConfigureAwait(false);
                    return;
                }

                if (SupportedFeaturesErrorCode is string supportedFeaturesErrorCode
                    && !string.IsNullOrWhiteSpace(supportedFeaturesErrorCode))
                {
                    if (OmitSupportedFeaturesErrorMessage)
                    {
                        await session.SendAsync(
                            new Dictionary<string, object?>
                            {
                                ["id"] = id,
                                ["type"] = "result",
                                ["success"] = false,
                                ["error"] = new Dictionary<string, object?> { ["code"] = supportedFeaturesErrorCode }
                            },
                            _source.Token).ConfigureAwait(false);
                    }
                    else
                    {
                        await session.SendErrorAsync(id, supportedFeaturesErrorCode, SupportedFeaturesErrorMessage, supportedFeaturesErrorCode, _source.Token).ConfigureAwait(false);
                    }
                    return;
                }

                session.MessageCoalescingEnabled = command.TryGetProperty("features", out var features)
                    && features.ValueKind == JsonValueKind.Object
                    && features.TryGetProperty("coalesce_messages", out var coalesce)
                    && coalesce.TryGetInt32(out var coalesceVersion)
                    && coalesceVersion == 1;
                var acknowledgement = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["type"] = "result"
                };
                if (!OmitSupportedFeaturesResult) acknowledgement["result"] = null;
                if (!string.Equals(SupportedFeaturesSuccessJson, "omit", StringComparison.Ordinal))
                {
                    acknowledgement["success"] = SupportedFeaturesSuccessJson is null
                        ? true
                        : ParseJson(SupportedFeaturesSuccessJson);
                }

                if (CoalesceSupportedFeaturesAcknowledgement)
                {
                    await session.SendCoalescedAsync(new object[] { acknowledgement }, _source.Token)
                        .ConfigureAwait(false);
                }
                else
                {
                    await session.SendAsync(acknowledgement, _source.Token).ConfigureAwait(false);
                }
                return;
            case "ping":
                await session.SendAsync(new Dictionary<string, object?> { ["id"] = id, ["type"] = "pong" }, _source.Token)
                    .ConfigureAwait(false);
                return;
            case "get_states":
                TaskCompletionSource<bool>? pausedGetStatesReceived;
                TaskCompletionSource<bool>? pausedGetStatesRelease;
                lock (_stateGate)
                {
                    pausedGetStatesReceived = _pausedGetStatesReceived;
                    pausedGetStatesRelease = _pausedGetStatesRelease;
                    _pausedGetStatesReceived = null;
                    _pausedGetStatesRelease = null;
                }
                if (pausedGetStatesReceived is not null && pausedGetStatesRelease is not null)
                {
                    pausedGetStatesReceived.TrySetResult(true);
                    await pausedGetStatesRelease.Task.ConfigureAwait(false);
                }

                if (SendStateChangeBeforeSnapshot && session.StateSubscriptionId is int subscriptionId)
                {
                    await session.SendStateChangeAsync(subscriptionId, _source.Token).ConfigureAwait(false);
                }

                await session.SendResultAsync(id, ParseJson(GetStates()), fragmented: true, _source.Token).ConfigureAwait(false);
                return;
            case "get_config":
                await session.SendResultAsync(id, ParseJson("{\"location_name\":\"Test Home\",\"version\":\"2026.8.3\",\"components\":[\"api\"]}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "get_services":
                await session.SendResultAsync(id, ParseJson(ActionCatalogResponseJson), false, _source.Token).ConfigureAwait(false);
                return;
            case "get_panels":
                await session.SendResultAsync(id, ParseJson("{\"lovelace\":{\"title\":\"Overview\"}}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "validate_config":
                await session.SendResultAsync(id, ParseJson("{\"trigger\":null,\"condition\":null,\"action\":null}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "extract_from_target":
                await session.SendResultAsync(id, ParseJson("{\"referenced_entities\":[\"light.kitchen\"],\"referenced_devices\":[],\"referenced_areas\":[]}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "get_triggers_for_target":
            case "get_conditions_for_target":
            case "get_services_for_target":
                await session.SendResultAsync(id, ParseJson("[{}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/entity_registry/list_for_display":
                await session.SendResultAsync(id, ParseJson("{\"entity_categories\":{\"0\":\"config\"},\"entities\":[{\"ei\":\"sensor.kitchen_temperature\",\"pl\":\"test\",\"en\":\"Kitchen temperature\"}]}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "homeassistant/expose_entity/list":
                await session.SendResultAsync(id, ParseJson("{\"exposed_entities\":{\"light.kitchen\":{\"conversation\":true}}}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "homeassistant/expose_entity":
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "auth/sign_path":
                var requestedPath = command.GetProperty("path").GetString()!;
                var signedPathResponse = SignedPathResponseJson
                    ?? JsonSerializer.Serialize(new { path = requestedPath + (requestedPath.IndexOf('?') >= 0 ? "&" : "?") + "authSig=signed" });
                await session.SendResultAsync(id, ParseJson(signedPathResponse), false, _source.Token).ConfigureAwait(false);
                return;
            case "auth/long_lived_access_token":
                await session.SendResultAsync(id, "fake-long-lived-token", false, _source.Token).ConfigureAwait(false);
                return;
            case "conversation/process":
                await session.SendResultAsync(id, ParseJson("{\"conversation_id\":\"conversation-1\",\"continue_conversation\":false}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "fire_event":
                await session.SendResultAsync(id, ParseJson("{\"context\":{\"id\":\"event-context\"}}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "subscribe_events":
                if (Interlocked.Exchange(ref _failNextSubscription, 0) != 0)
                {
                    await session.SendErrorAsync(
                        id,
                        "temporary_subscription_failure",
                        "Subscription is temporarily unavailable.",
                        "temporary_subscription_failure",
                        _source.Token).ConfigureAwait(false);
                    return;
                }

                var pauseReceived = _pausedSubscriptionReceived;
                var pauseRelease = _pausedSubscriptionRelease;
                if (pauseReceived is not null && pauseRelease is not null)
                {
                    pauseReceived.TrySetResult(true);
                    await pauseRelease.Task.ConfigureAwait(false);
                    _pausedSubscriptionReceived = null;
                    _pausedSubscriptionRelease = null;
                }

                session.SubscriptionIds.Add(id);
                Volatile.Write(ref _lastSubscriptionSessionId, sessionId);
                session.StateSubscriptionId = command.TryGetProperty("event_type", out var eventType)
                    && eventType.GetString() == "state_changed"
                    ? id
                    : session.StateSubscriptionId;
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "unsubscribe_events":
                var unsubscribeSubscriptionId = command.GetProperty("subscription").GetInt32();
                var validUnsubscribe = session.SubscriptionIds.Remove(unsubscribeSubscriptionId);
                Interlocked.Increment(ref _unsubscribeCommandCount);
                Volatile.Write(ref _lastUnsubscribeSessionId, sessionId);
                if (validUnsubscribe)
                {
                    _unsubscribeReceived.TrySetResult(true);
                    if (session.StateSubscriptionId == unsubscribeSubscriptionId)
                    {
                        session.StateSubscriptionId = null;
                    }
                }
                else
                {
                    Interlocked.Increment(ref _invalidUnsubscribeCommandCount);
                }

                if (IgnoreUnsubscribeAcknowledgement)
                {
                    return;
                }

                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "call_service":
                LastServiceCallBody = command.GetRawText();
                _serviceCallBodies.Enqueue(LastServiceCallBody);
                TaskCompletionSource<bool>? pausedReceived;
                TaskCompletionSource<bool>? pausedRelease;
                lock (_stateGate)
                {
                    pausedReceived = _pausedServiceCallReceived;
                    pausedRelease = _pausedServiceCallRelease;
                    _pausedServiceCallReceived = null;
                    _pausedServiceCallRelease = null;
                }
                if (pausedReceived is not null && pausedRelease is not null)
                {
                    pausedReceived.TrySetResult(true);
                    var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    using (_source.Token.Register(() => canceled.TrySetCanceled()))
                    {
                        var completed = await Task.WhenAny(pausedRelease.Task, canceled.Task).ConfigureAwait(false);
                        await completed.ConfigureAwait(false);
                    }
                }
                await session.SendResultAsync(id, new Dictionary<string, object?>
                {
                    ["context"] = new Dictionary<string, object?> { ["id"] = "service-context" },
                    ["response"] = new Dictionary<string, object?> { ["accepted"] = true }
                }, false, _source.Token).ConfigureAwait(false);
                return;
            case "persistent_notification/get":
                await session.SendResultAsync(id, ParseJson("[{\"notification_id\":\"notice-1\",\"message\":\"Door open\",\"title\":\"Security\",\"created_at\":\"2026-08-26T08:00:00Z\",\"source\":\"fixture\"}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "persistent_notification/subscribe":
                session.SubscriptionIds.Add(id);
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                await session.SendSubscriptionEventAsync(id, ParseJson("{\"type\":\"current\",\"notifications\":{\"notice-1\":{\"notification_id\":\"notice-1\",\"message\":\"Door open\",\"title\":\"Security\",\"created_at\":\"2026-08-26T08:00:00Z\"}}}"), _source.Token).ConfigureAwait(false);
                return;
            case "calendar/event/create":
            case "calendar/event/update":
            case "calendar/event/delete":
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "calendar/event/subscribe":
                session.SubscriptionIds.Add(id);
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                await session.SendSubscriptionEventAsync(id, ParseJson("[{\"summary\":\"Dinner\",\"start\":\"2026-08-26T18:00:00+02:00\",\"end\":\"2026-08-26T20:00:00+02:00\",\"uid\":\"event-1\",\"rrule\":\"FREQ=WEEKLY\"}]"), _source.Token).ConfigureAwait(false);
                return;
            case "test/error":
                await session.SendErrorAsync(id, "service_validation_error", "Option is not supported.", "unsupported_option", _source.Token)
                    .ConfigureAwait(false);
                return;
            case "test/slow":
                _ = Task.Run(async () =>
                {
                    await Task.Delay(100).ConfigureAwait(false);
                    await session.SendResultAsync(id, new Dictionary<string, object?> { ["value"] = "slow" }, false, _source.Token)
                        .ConfigureAwait(false);
                });
                return;
            case "test/fast":
                await session.SendResultAsync(id, new Dictionary<string, object?> { ["value"] = "fast" }, false, _source.Token)
                    .ConfigureAwait(false);
                return;
            case "test/coalesced":
                if (!session.MessageCoalescingEnabled)
                {
                    await session.SendErrorAsync(
                        id,
                        "not_supported",
                        "Message coalescing was not enabled.",
                        "not_supported",
                        _source.Token).ConfigureAwait(false);
                    return;
                }

                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["type"] = "test_notice",
                            ["value"] = "before-result"
                        },
                        new Dictionary<string, object?>
                        {
                            ["id"] = id,
                            ["type"] = "result",
                            ["success"] = true,
                            ["result"] = new Dictionary<string, object?> { ["value"] = "coalesced" }
                        }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/malformed_coalesced":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["id"] = id,
                            ["type"] = "result",
                            ["success"] = true,
                            ["result"] = null
                        },
                        42
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/empty_coalesced":
                await session.SendCoalescedAsync(Array.Empty<object>(), _source.Token).ConfigureAwait(false);
                return;
            case "test/coalesced_blank_type":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?> { ["type"] = "   " },
                        new Dictionary<string, object?> { ["id"] = id, ["type"] = "result", ["success"] = true, ["result"] = null }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/coalesced_invalid_id":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?> { ["type"] = "event", ["event"] = new Dictionary<string, object?>() },
                        new Dictionary<string, object?> { ["id"] = id, ["type"] = "result", ["success"] = true, ["result"] = null }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/coalesced_zero_id":
            case "test/coalesced_negative_id":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["id"] = string.Equals(type, "test/coalesced_zero_id", StringComparison.Ordinal) ? 0 : -1,
                            ["type"] = "result",
                            ["success"] = true,
                            ["result"] = null
                        }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/coalesced_missing_success":
            case "test/coalesced_null_success":
            case "test/coalesced_string_success":
                var malformedCoalescedResult = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["type"] = "result",
                    ["result"] = null
                };
                if (!string.Equals(type, "test/coalesced_missing_success", StringComparison.Ordinal))
                {
                    malformedCoalescedResult["success"] = string.Equals(type, "test/coalesced_null_success", StringComparison.Ordinal)
                        ? null
                        : "true";
                }
                await session.SendCoalescedAsync(new object[] { malformedCoalescedResult }, _source.Token).ConfigureAwait(false);
                return;
            case "test/coalesced_missing_error":
            case "test/coalesced_malformed_error":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["id"] = id,
                            ["type"] = "result",
                            ["success"] = false,
                            ["error"] = string.Equals(type, "test/coalesced_missing_error", StringComparison.Ordinal)
                                ? null
                                : new Dictionary<string, object?> { ["code"] = 1, ["message"] = "failed" }
                        }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/standalone_missing_success":
            case "test/standalone_null_success":
            case "test/standalone_string_success":
                var malformedStandaloneResult = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["type"] = "result",
                    ["result"] = null
                };
                if (!string.Equals(type, "test/standalone_missing_success", StringComparison.Ordinal))
                {
                    malformedStandaloneResult["success"] = string.Equals(type, "test/standalone_null_success", StringComparison.Ordinal)
                        ? null
                        : "true";
                }
                await session.SendAsync(malformedStandaloneResult, _source.Token).ConfigureAwait(false);
                return;
            case "test/standalone_missing_error":
            case "test/standalone_malformed_error":
                await session.SendAsync(
                    new Dictionary<string, object?>
                    {
                        ["id"] = id,
                        ["type"] = "result",
                        ["success"] = false,
                        ["error"] = string.Equals(type, "test/standalone_missing_error", StringComparison.Ordinal)
                            ? null
                            : new Dictionary<string, object?> { ["code"] = "failed", ["message"] = 1 }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/standalone_zero_id":
            case "test/standalone_negative_id":
                await session.SendAsync(
                    new Dictionary<string, object?>
                    {
                        ["id"] = string.Equals(type, "test/standalone_zero_id", StringComparison.Ordinal) ? 0 : -1,
                        ["type"] = "result",
                        ["success"] = true,
                        ["result"] = null
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/standalone_blank_type":
                await session.SendAsync(
                    new Dictionary<string, object?> { ["type"] = string.Empty },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/coalesced_missing_event":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = id, ["type"] = "event" },
                        new Dictionary<string, object?>
                        {
                            ["id"] = id,
                            ["type"] = "result",
                            ["success"] = true,
                            ["result"] = new Dictionary<string, object?> { ["value"] = "must-not-route" }
                        }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/coalesced_null_event":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = id, ["type"] = "event", ["event"] = null },
                        new Dictionary<string, object?> { ["id"] = id, ["type"] = "result", ["success"] = true, ["result"] = null }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/coalesced_duplicate_terminal_id":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = id, ["type"] = "result", ["success"] = true, ["result"] = null },
                        new Dictionary<string, object?> { ["id"] = id, ["type"] = "pong" }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/forced_coalesced":
                await session.SendCoalescedAsync(
                    new object[]
                    {
                        new Dictionary<string, object?> { ["id"] = id, ["type"] = "result", ["success"] = true, ["result"] = null }
                    },
                    _source.Token).ConfigureAwait(false);
                return;
            case "test/missing_event":
                await session.SendAsync(
                    new Dictionary<string, object?> { ["id"] = id, ["type"] = "event" },
                    _source.Token).ConfigureAwait(false);
                return;
            case "config/area_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"area_id\":\"kitchen\",\"name\":\"Kitchen\",\"aliases\":[\"Cooking\"],\"floor_id\":\"ground\",\"labels\":[\"security\"]}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/label_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"label_id\":\"security\",\"name\":\"Security\",\"color\":\"red\",\"description\":\"Safety devices\",\"icon\":\"mdi:shield\",\"created_at\":1787731200,\"modified_at\":1787731300}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/label_registry/create":
            case "config/label_registry/update":
                await session.SendResultAsync(id, ParseJson("{\"label_id\":\"security\",\"name\":\"Security\",\"color\":null,\"description\":\"Safety devices\",\"icon\":\"mdi:shield\"}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/label_registry/delete":
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "config/category_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"category_id\":\"comfort\",\"name\":\"Comfort\",\"icon\":\"mdi:sofa\",\"created_at\":1787731200,\"modified_at\":1787731300}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/category_registry/create":
            case "config/category_registry/update":
                await session.SendResultAsync(id, ParseJson("{\"category_id\":\"comfort\",\"name\":\"Comfort\",\"icon\":null}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/category_registry/delete":
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "config/floor_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"floor_id\":\"ground\",\"name\":\"Ground\",\"aliases\":[\"Downstairs\"],\"level\":0}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/device_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"id\":\"device-1\",\"area_id\":\"kitchen\",\"name\":\"Kitchen Sensor\",\"manufacturer\":\"Evotec\",\"config_entries\":[\"entry-1\"]}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/entity_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"entity_id\":\"sensor.kitchen_temperature\",\"unique_id\":\"temperature-1\",\"platform\":\"test\",\"device_id\":\"device-1\",\"config_entry_id\":\"entry-1\",\"has_entity_name\":true},{\"entity_id\":\"light.kitchen\",\"unique_id\":\"light-1\",\"platform\":\"test\",\"device_id\":\"device-1\",\"config_entry_id\":\"entry-1\",\"name\":\"Light\",\"has_entity_name\":true,\"list_only\":{\"source\":\"partial\"}},{\"entity_id\":\"sensor.disabled_temperature\",\"unique_id\":\"temperature-2\",\"platform\":\"test\",\"device_id\":\"device-1\",\"config_entry_id\":\"entry-1\",\"original_name\":\"Temperature\",\"has_entity_name\":true,\"disabled_by\":\"integration\"},{\"entity_id\":\"sensor.legacy_disabled\",\"unique_id\":\"legacy-1\",\"platform\":\"test\",\"device_id\":\"device-1\",\"config_entry_id\":\"entry-1\",\"original_name\":\"Kitchen legacy temperature\",\"has_entity_name\":false,\"disabled_by\":\"integration\"}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/entity_registry/get_entries":
                await session.SendResultAsync(id, ParseJson(ExtendedEntityRegistryResponseJson
                    ?? "{\"sensor.kitchen_temperature\":{\"entity_id\":\"sensor.kitchen_temperature\",\"unique_id\":\"temperature-1\",\"platform\":\"test\",\"device_id\":\"device-1\",\"config_entry_id\":\"entry-1\",\"has_entity_name\":true,\"aliases\":[null],\"device_class\":\"temperature\",\"capabilities\":{}},\"light.kitchen\":{\"entity_id\":\"light.kitchen\",\"unique_id\":\"light-1\",\"platform\":\"test\",\"device_id\":\"device-1\",\"config_entry_id\":\"entry-1\",\"name\":\"Light\",\"has_entity_name\":true,\"aliases\":[null,\"Island fixture\"],\"capabilities\":{},\"extended_only\":true},\"sensor.disabled_temperature\":{\"entity_id\":\"sensor.disabled_temperature\",\"unique_id\":\"temperature-2\",\"platform\":\"test\",\"device_id\":\"device-1\",\"config_entry_id\":\"entry-1\",\"original_name\":\"Temperature\",\"has_entity_name\":true,\"disabled_by\":\"integration\",\"aliases\":[null],\"device_class\":\"temperature\",\"capabilities\":{}},\"sensor.legacy_disabled\":{\"entity_id\":\"sensor.legacy_disabled\",\"unique_id\":\"legacy-1\",\"platform\":\"test\",\"device_id\":\"device-1\",\"config_entry_id\":\"entry-1\",\"original_name\":\"Kitchen legacy temperature\",\"has_entity_name\":false,\"disabled_by\":\"integration\",\"aliases\":[null],\"device_class\":\"temperature\",\"capabilities\":{}}}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config_entries/get":
                if (!string.IsNullOrWhiteSpace(ConfigEntriesErrorCode))
                {
                    var errorCode = ConfigEntriesErrorCode!;
                    await session.SendErrorAsync(id, errorCode, "Configuration entries unavailable", errorCode, _source.Token).ConfigureAwait(false);
                    return;
                }

                await session.SendResultAsync(id, ParseJson("{\"entries\":[{\"entry_id\":\"entry-1\",\"domain\":\"test\",\"title\":\"Test integration\",\"source\":\"user\",\"state\":\"loaded\",\"supports_unload\":true,\"supports_reconfigure\":true,\"disabled_by\":null}]}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config_entries/get_single":
                await session.SendResultAsync(id, ParseJson("{\"config_entry\":{\"entry_id\":\"entry-1\",\"domain\":\"test\",\"title\":\"Test integration\",\"source\":\"user\",\"state\":\"loaded\",\"supports_unload\":true}}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config_entries/disable":
                await session.SendResultAsync(id, ParseJson("{\"require_restart\":false}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "system_log/list":
                await session.SendResultAsync(id, ParseJson("[{\"name\":\"homeassistant.components.test\",\"message\":[\"Test warning\"],\"level\":\"WARNING\",\"source\":[\"homeassistant/components/test/__init__.py\",42],\"exception\":\"test exception\",\"count\":2,\"timestamp\":1787680800,\"first_occurred\":1787680700}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "repairs/list_issues":
                await session.SendResultAsync(id, ParseJson("{\"issues\":[{\"domain\":\"test\",\"issue_id\":\"warning-1\",\"active\":true,\"is_fixable\":true,\"severity\":\"warning\",\"ignored\":false,\"created\":\"2026-08-25T10:00:00Z\"},{\"domain\":\"test\",\"issue_id\":\"ignored-1\",\"active\":true,\"is_fixable\":false,\"severity\":\"warning\",\"ignored\":true,\"created\":\"2026-08-25T09:00:00Z\"}]}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "repairs/get_issue_data":
                await session.SendResultAsync(id, ParseJson("{\"issue_data\":{\"summary\":\"Test repair\"}}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "repairs/ignore_issue":
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "diagnostics/list":
                await session.SendResultAsync(id, ParseJson("[{\"domain\":\"test\",\"handlers\":{\"config_entry\":true,\"device\":true}}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "trace/list":
                await session.SendResultAsync(id, ParseJson("[{\"domain\":\"automation\",\"item_id\":\"night\",\"run_id\":\"run-1\",\"state\":\"stopped\",\"script_execution\":\"error\",\"last_step\":\"action/0\",\"error\":\"Test failure\",\"timestamp\":{\"start\":\"2026-08-25T10:00:00Z\",\"finish\":\"2026-08-25T10:00:01Z\"}}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "trace/get":
                await session.SendResultAsync(id, ParseJson("{\"domain\":\"automation\",\"item_id\":\"night\",\"run_id\":\"run-1\",\"state\":\"stopped\",\"trace\":{\"action/0\":[{\"path\":\"action/0\",\"error\":\"Test failure\"}]}}"), false, _source.Token).ConfigureAwait(false);
                return;
            case "update/release_notes":
                await session.SendResultAsync(
                    id,
                    ReturnInvalidUpdateReleaseNotes ? 42 : "Test release notes",
                    false,
                    _source.Token).ConfigureAwait(false);
                return;
            case "system_health/info":
                session.SubscriptionIds.Add(id);
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                await session.SendSubscriptionEventAsync(id, ParseJson(SystemHealthInitialEventJson), _source.Token).ConfigureAwait(false);
                await session.SendSubscriptionEventAsync(id, ParseJson("{\"type\":\"update\",\"success\":true,\"domain\":\"homeassistant\",\"key\":\"python_version\",\"data\":\"3.14.1\"}"), _source.Token).ConfigureAwait(false);
                await session.SendSubscriptionEventAsync(id, ParseJson("{\"type\":\"update\",\"success\":false,\"domain\":\"test\",\"key\":\"api\",\"error\":{\"msg\":\"Unavailable\"}}"), _source.Token).ConfigureAwait(false);
                _systemHealthEventsSent.TrySetResult(true);
                if (!OmitSystemHealthFinish)
                {
                    await session.SendSubscriptionEventAsync(id, ParseJson("{\"type\":\"finish\"}"), _source.Token).ConfigureAwait(false);
                }
                return;
            case "supervisor/api":
                await HandleSupervisorWebSocketCommandAsync(session, id, command).ConfigureAwait(false);
                return;
            default:
                await session.SendResultAsync(id, new Dictionary<string, object?> { ["echo_type"] = type }, false, _source.Token)
                    .ConfigureAwait(false);
                return;
        }
    }

    private async Task HandleSupervisorWebSocketCommandAsync(SocketSession session, int id, JsonElement command)
    {
        var endpoint = command.GetProperty("endpoint").GetString();
        object response = endpoint switch
        {
            "/supervisor/info" => ParseJson("{\"version\":\"2026.08.0\",\"version_latest\":\"2026.08.1\",\"update_available\":true,\"arch\":\"amd64\",\"channel\":\"stable\",\"healthy\":true,\"supported\":true,\"timezone\":\"Europe/Warsaw\"}"),
            "/info" => ParseJson("{\"supervisor\":\"2026.08.0\",\"homeassistant\":\"2026.8.3\",\"hassos\":\"17.0\",\"hostname\":\"test-host\",\"operating_system\":\"Home Assistant OS\",\"machine\":\"generic-x86-64\",\"arch\":\"amd64\",\"supported\":true,\"channel\":\"stable\",\"state\":\"running\",\"features\":[\"reboot\"]}"),
            "/core/info" => ParseJson("{\"version\":\"2026.8.3\",\"version_latest\":\"2026.8.4\",\"update_available\":true}"),
            "/available_updates" => ParseJson("{\"available_updates\":[{\"update_type\":\"core\",\"version_latest\":\"2026.8.4\",\"panel_path\":\"/update-available/core\"}]}"),
            "/addons" => ParseJson("{\"addons\":[{\"slug\":\"test_app\",\"name\":\"Test app\",\"version\":\"1.0.0\",\"version_latest\":\"1.1.0\",\"state\":\"started\",\"update_available\":true,\"repository\":\"core\"}]}"),
            "/backups" => ParseJson("{\"backups\":[{\"slug\":\"backup-1\",\"date\":\"2026-08-25T08:00:00Z\",\"name\":\"Before update\",\"type\":\"full\",\"size\":42.5,\"protected\":true,\"compressed\":true,\"location\":null,\"content\":{\"homeassistant\":true}}]}"),
            "/jobs/info" => ParseJson("{\"ignore_conditions\":[],\"jobs\":[{\"uuid\":\"job-1\",\"name\":\"backup_manager_full_backup\",\"reference\":\"backup-1\",\"progress\":100,\"stage\":\"done\",\"done\":true,\"extra\":null}]}"),
            "/jobs/job-1" => ParseJson("{\"uuid\":\"job-1\",\"name\":\"backup_manager_full_backup\",\"reference\":\"backup-1\",\"progress\":100,\"stage\":\"done\",\"done\":true,\"extra\":null}"),
            "/resolution/info" => ParseJson("{\"issues\":[{\"uuid\":\"resolution-1\",\"type\":\"unsupported\",\"context\":\"system\"}],\"suggestions\":[],\"checks\":[]}"),
            "/backups/new/full" => ParseJson("{\"slug\":\"backup-new\",\"job_id\":\"job-new\"}"),
            _ when endpoint is not null && (endpoint.EndsWith("/restart", StringComparison.Ordinal)
                || endpoint.EndsWith("/reboot", StringComparison.Ordinal)
                || endpoint.EndsWith("/update", StringComparison.Ordinal)
                || endpoint.EndsWith("/install", StringComparison.Ordinal)
                || endpoint.EndsWith("/start", StringComparison.Ordinal)
                || endpoint.EndsWith("/stop", StringComparison.Ordinal)
                || endpoint.EndsWith("/uninstall", StringComparison.Ordinal)) => ParseJson("{}"),
            _ => ParseJson("{}")
        };
        await session.SendResultAsync(id, response, false, _source.Token).ConfigureAwait(false);
    }

    private string GetStates()
    {
        lock (_stateGate)
        {
            return _statesJson;
        }
    }

    private static async Task WriteHttpResponseAsync(NetworkStream stream, int statusCode, string body)
    {
        var bodyBytes = Encoding.UTF8.GetBytes(body);
        var statusText = statusCode switch
        {
            200 => "OK",
            400 => "Bad Request",
            401 => "Unauthorized",
            404 => "Not Found",
            _ => "Error"
        };
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 " + statusCode + " " + statusText + "\r\n"
            + "Content-Type: application/json\r\n"
            + "Content-Length: " + bodyBytes.Length + "\r\n"
            + "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
        await stream.WriteAsync(bodyBytes, 0, bodyBytes.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
    }

    private async Task WriteHeadersAndStallAsync(NetworkStream stream, int contentLength)
    {
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n"
            + "Content-Type: application/octet-stream\r\n"
            + "Content-Length: " + contentLength + "\r\n"
            + "Connection: close\r\n\r\n");
        await stream.WriteAsync(header, 0, header.Length).ConfigureAwait(false);
        await stream.FlushAsync().ConfigureAwait(false);
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), _source.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_source.IsCancellationRequested)
        {
        }
    }

    private static string ComputeWebSocketAccept(string key)
    {
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(Encoding.ASCII.GetBytes(key + WebSocketMagic));
        return Convert.ToBase64String(hash);
    }

    private static object? ParseOptional(string? json)
    {
        return json is null ? null : ParseJson(json);
    }

    private static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _source.Cancel();
        _pausedSubscriptionRelease?.TrySetCanceled();
        _listener.Stop();
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }

        try
        {
            _acceptTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _source.Dispose();
    }

    public const string KitchenTemperatureStateJson =
        "{\"entity_id\":\"sensor.kitchen_temperature\",\"state\":\"21.5\",\"attributes\":{\"friendly_name\":\"Kitchen temperature\",\"unit_of_measurement\":\"°C\",\"nested\":{\"quality\":\"good\"}},\"last_changed\":\"2026-08-25T12:00:00+00:00\",\"last_reported\":\"2026-08-25T12:00:01+00:00\",\"last_updated\":\"2026-08-25T12:00:01+00:00\",\"context\":{\"id\":\"context-1\",\"parent_id\":null,\"user_id\":null,\"trace_hint\":\"state-trace\"},\"custom_state_field\":{\"source\":\"test\"}}";

    public const string KitchenLightOffStateJson =
        "{\"entity_id\":\"light.kitchen\",\"state\":\"off\",\"attributes\":{\"friendly_name\":\"Kitchen light\",\"supported_color_modes\":[\"brightness\"]},\"last_changed\":\"2026-08-25T12:00:00+00:00\",\"last_updated\":\"2026-08-25T12:00:00+00:00\",\"context\":{\"id\":\"context-2\"}}";

    public const string KitchenLightOnStateJson =
        "{\"entity_id\":\"light.kitchen\",\"state\":\"on\",\"attributes\":{\"friendly_name\":\"Kitchen light\",\"brightness\":180},\"last_changed\":\"2026-08-25T12:00:02+00:00\",\"last_updated\":\"2026-08-25T12:00:02+00:00\",\"context\":{\"id\":\"context-3\"}}";

    public const string LivingRoomRemoteStateJson =
        "{\"entity_id\":\"remote.living_room\",\"state\":\"on\",\"attributes\":{\"friendly_name\":\"Living room remote\"}}";

    public const string DefaultStatesJson = "[" + KitchenTemperatureStateJson + "," + KitchenLightOffStateJson + "]";

    private sealed class SocketSession : IDisposable
    {
        private readonly WebSocket _socket;
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private int _disposed;

        public SocketSession(WebSocket socket)
        {
            _socket = socket;
        }

        public int? StateSubscriptionId { get; set; }

        public bool MessageCoalescingEnabled { get; set; }

        public HashSet<int> SubscriptionIds { get; } = new();

        public async Task<string> ReceiveAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            using var stream = new MemoryStream();
            while (true)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
                }

                stream.Write(buffer, 0, result.Count);
                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }

        public Task SendResultAsync(int id, object? result, bool fragmented, CancellationToken cancellationToken)
        {
            return SendAsync(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["type"] = "result",
                ["success"] = true,
                ["result"] = result
            }, cancellationToken, fragmented);
        }

        public Task SendErrorAsync(
            int id,
            string code,
            string message,
            string translationKey,
            CancellationToken cancellationToken)
        {
            return SendAsync(new Dictionary<string, object?>
            {
                ["id"] = id,
                ["type"] = "result",
                ["success"] = false,
                ["error"] = new Dictionary<string, object?>
                {
                    ["code"] = code,
                    ["message"] = message,
                    ["translation_key"] = translationKey
                }
            }, cancellationToken);
        }

        public Task SendStateChangeAsync(int subscriptionId, CancellationToken cancellationToken)
        {
            return SendAsync(new Dictionary<string, object?>
            {
                ["id"] = subscriptionId,
                ["type"] = "event",
                ["event"] = new Dictionary<string, object?>
                {
                    ["event_type"] = "state_changed",
                    ["data"] = new Dictionary<string, object?>
                    {
                        ["entity_id"] = "light.kitchen",
                        ["old_state"] = ParseJson(KitchenLightOffStateJson),
                        ["new_state"] = ParseJson(KitchenLightOnStateJson)
                    },
                    ["origin"] = "LOCAL",
                    ["time_fired"] = DateTimeOffset.UtcNow,
                    ["context"] = new Dictionary<string, object?> { ["id"] = "buffered-event" }
                }
            }, cancellationToken);
        }

        public Task SendSubscriptionEventAsync(
            int subscriptionId,
            JsonElement payload,
            CancellationToken cancellationToken)
        {
            return SendAsync(new Dictionary<string, object?>
            {
                ["id"] = subscriptionId,
                ["type"] = "event",
                ["event"] = payload
            }, cancellationToken);
        }

        public Task SendAsync(object payload, CancellationToken cancellationToken)
        {
            return SendAsync(payload, cancellationToken, false);
        }

        public Task SendCoalescedAsync(object[] payloads, CancellationToken cancellationToken)
        {
            return SendAsync(payloads, cancellationToken, false);
        }

        public async Task SendTextAsync(string payload, CancellationToken cancellationToken)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        private async Task SendAsync(object payload, CancellationToken cancellationToken, bool fragmented)
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
            await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (!fragmented || bytes.Length < 2)
                {
                    await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken)
                        .ConfigureAwait(false);
                    return;
                }

                var split = bytes.Length / 2;
                await _socket.SendAsync(new ArraySegment<byte>(bytes, 0, split), WebSocketMessageType.Text, false, cancellationToken)
                    .ConfigureAwait(false);
                await _socket.SendAsync(new ArraySegment<byte>(bytes, split, bytes.Length - split), WebSocketMessageType.Text, true, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public Task AbortAsync()
        {
            _socket.Abort();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _socket.Abort();
            _socket.Dispose();
            _sendGate.Dispose();
        }
    }
}
