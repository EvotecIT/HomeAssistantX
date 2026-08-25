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
    private int _unsubscribeCommandCount;
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

    public string? LastAuthorization { get; private set; }

    public string? LastServiceCallBody { get; private set; }

    public string? LastRequestBody { get; private set; }

    public string? LastRequestPath { get; private set; }

    public int OAuthTokenRequestCount { get; private set; }

    public string? LastRevokedRefreshToken { get; private set; }

    public int UnsubscribeCommandCount => Volatile.Read(ref _unsubscribeCommandCount);

    public string? GetLastWebSocketCommand(string commandType)
    {
        return _lastWebSocketCommands.TryGetValue(commandType, out var command) ? command : null;
    }

    public bool SendStateChangeBeforeSnapshot { get; set; }

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

    public async Task PublishStateChangeAsync(
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
        foreach (var session in _sessions.Values)
        {
            if (session.StateSubscriptionId is int subscriptionId)
            {
                await session.SendAsync(new Dictionary<string, object?>
                {
                    ["id"] = subscriptionId,
                    ["type"] = "event",
                    ["event"] = new Dictionary<string, object?>
                    {
                        ["event_type"] = "state_changed",
                        ["data"] = data,
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
                    && token.GetString() == AccessToken;
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
                await HandleWebSocketCommandAsync(session, id, type, command).ConfigureAwait(false);
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
        SocketSession session,
        int id,
        string type,
        JsonElement command)
    {
        _lastWebSocketCommands[type] = command.GetRawText();
        switch (type)
        {
            case "ping":
                await session.SendAsync(new Dictionary<string, object?> { ["id"] = id, ["type"] = "pong" }, _source.Token)
                    .ConfigureAwait(false);
                return;
            case "get_states":
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
                await session.SendResultAsync(id, ParseJson("{\"light\":{\"turn_on\":{\"name\":\"Turn on\"}}}"), false, _source.Token).ConfigureAwait(false);
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
                await session.SendResultAsync(id, ParseJson("{\"path\":\"/api/camera_proxy/camera.front?authSig=signed\"}"), false, _source.Token).ConfigureAwait(false);
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
                    _pausedSubscriptionReceived = null;
                    pauseReceived.TrySetResult(true);
                    await pauseRelease.Task.ConfigureAwait(false);
                    _pausedSubscriptionRelease = null;
                }

                session.StateSubscriptionId = command.TryGetProperty("event_type", out var eventType)
                    && eventType.GetString() == "state_changed"
                    ? id
                    : session.StateSubscriptionId;
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "unsubscribe_events":
                Interlocked.Increment(ref _unsubscribeCommandCount);
                session.StateSubscriptionId = null;
                await session.SendResultAsync(id, null, false, _source.Token).ConfigureAwait(false);
                return;
            case "call_service":
                LastServiceCallBody = command.GetRawText();
                await session.SendResultAsync(id, new Dictionary<string, object?>
                {
                    ["context"] = new Dictionary<string, object?> { ["id"] = "service-context" },
                    ["response"] = new Dictionary<string, object?> { ["accepted"] = true }
                }, false, _source.Token).ConfigureAwait(false);
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
            case "config/area_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"area_id\":\"kitchen\",\"name\":\"Kitchen\",\"floor_id\":\"ground\"}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/floor_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"floor_id\":\"ground\",\"name\":\"Ground\",\"level\":0}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/device_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"id\":\"device-1\",\"area_id\":\"kitchen\",\"name\":\"Kitchen Sensor\",\"manufacturer\":\"Evotec\"}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config/entity_registry/list":
                await session.SendResultAsync(id, ParseJson("[{\"entity_id\":\"sensor.kitchen_temperature\",\"unique_id\":\"temperature-1\",\"platform\":\"test\",\"device_id\":\"device-1\"}]"), false, _source.Token).ConfigureAwait(false);
                return;
            case "config_entries/get":
                await session.SendResultAsync(id, ParseJson("{\"entries\":[{\"entry_id\":\"entry-1\",\"domain\":\"test\",\"title\":\"Test integration\",\"state\":\"loaded\"}]}"), false, _source.Token).ConfigureAwait(false);
                return;
            default:
                await session.SendResultAsync(id, new Dictionary<string, object?> { ["echo_type"] = type }, false, _source.Token)
                    .ConfigureAwait(false);
                return;
        }
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

        public Task SendAsync(object payload, CancellationToken cancellationToken)
        {
            return SendAsync(payload, cancellationToken, false);
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
