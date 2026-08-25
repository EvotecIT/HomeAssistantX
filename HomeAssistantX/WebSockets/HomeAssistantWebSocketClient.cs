using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HomeAssistantX.Configuration;
using HomeAssistantX.Diagnostics;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.WebSockets;

/// <summary>
/// Multiplexes Home Assistant WebSocket commands and subscriptions over one authenticated connection.
/// Active subscriptions are restored after an unexpected disconnect.
/// </summary>
public sealed partial class HomeAssistantWebSocketClient : IDisposable
{
    private readonly HomeAssistantClientOptions _options;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly ConcurrentDictionary<Guid, SubscriptionRegistration> _subscriptions = new();
    private readonly ConcurrentDictionary<int, SubscriptionRegistration> _serverSubscriptions = new();
    private readonly CancellationTokenSource _disposeSource = new();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _connectionSource;
    private Task? _receiveTask;
    private Task? _reconnectTask;
    private int _nextCommandId;
    private int _disposed;
    private bool _manualDisconnect;
    private HomeAssistantConnectionState _state = HomeAssistantConnectionState.Disconnected;

    public HomeAssistantWebSocketClient(HomeAssistantClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
    }

    public event EventHandler<HomeAssistantConnectionStateChangedEventArgs>? ConnectionStateChanged;

    public HomeAssistantConnectionState State => _state;

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_socket?.State == WebSocketState.Open)
            {
                return;
            }

            _manualDisconnect = false;
            var reconnecting = _state == HomeAssistantConnectionState.Reconnecting;
            SetState(reconnecting ? HomeAssistantConnectionState.Reconnecting : HomeAssistantConnectionState.Connecting);

            var socket = new ClientWebSocket();
            socket.Options.KeepAliveInterval = _options.KeepAliveInterval;
            var connectionSource = CancellationTokenSource.CreateLinkedTokenSource(_disposeSource.Token);
            using var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _disposeSource.Token);
            connectTimeout.CancelAfter(_options.ConnectTimeout);
            try
            {
                await socket.ConnectAsync(HomeAssistantUri.BuildWebSocketUri(_options.BaseUri), connectTimeout.Token)
                    .ConfigureAwait(false);
                await AuthenticateAsync(socket, connectTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                socket.Dispose();
                connectionSource.Dispose();
                SetState(HomeAssistantConnectionState.Faulted, ex);
                if (ex is OperationCanceledException
                    && !cancellationToken.IsCancellationRequested
                    && !_disposeSource.IsCancellationRequested)
                {
                    throw new HomeAssistantConnectionException(
                        "The Home Assistant WebSocket connection timed out.",
                        new TimeoutException());
                }

                if (ex is HomeAssistantException || ex is OperationCanceledException)
                {
                    throw;
                }

                throw new HomeAssistantConnectionException("The Home Assistant WebSocket connection failed.", ex);
            }

            _socket = socket;
            _connectionSource = connectionSource;
            _receiveTask = ReceiveLoopAsync(socket, connectionSource);
            _serverSubscriptions.Clear();
            try
            {
                foreach (var registration in _subscriptions.Values)
                {
                    await ActivateSubscriptionAsync(registration, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(_socket, socket))
                {
                    _socket = null;
                }

                if (ReferenceEquals(_connectionSource, connectionSource))
                {
                    _connectionSource = null;
                }

                try
                {
                    connectionSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }

                try
                {
                    socket.Abort();
                }
                catch (ObjectDisposedException)
                {
                }

                socket.Dispose();
                _serverSubscriptions.Clear();
                SetState(HomeAssistantConnectionState.Faulted, ex);
                throw;
            }

            SetState(HomeAssistantConnectionState.Connected);
            WriteDiagnostic(HomeAssistantDiagnosticLevel.Information, "websocket.connected", "Connected to Home Assistant WebSocket API.");
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _manualDisconnect = true;
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var socket = _socket;
            _connectionSource?.Cancel();
            if (socket is not null && socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (WebSocketException)
                {
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }

            socket?.Dispose();
            _socket = null;
            _connectionSource = null;
            _serverSubscriptions.Clear();
            FailPendingRequests(new HomeAssistantConnectionException(
                "The Home Assistant WebSocket connection was closed.",
                new WebSocketException(WebSocketError.ConnectionClosedPrematurely)));
            SetState(HomeAssistantConnectionState.Disconnected);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async Task<JsonElement> RequestAsync(
        string commandType,
        IReadOnlyDictionary<string, object?>? payload = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandType))
        {
            throw new ArgumentException("A WebSocket command type is required.", nameof(commandType));
        }

        await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var commandId = NextCommandId();
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pendingRequests.TryAdd(commandId, completion))
        {
            throw new HomeAssistantProtocolException("A duplicate WebSocket command identifier was generated.");
        }

        try
        {
            await SendCommandAsync(commandId, commandType, payload, cancellationToken).ConfigureAwait(false);
            return await AwaitWithTimeoutAsync(completion.Task, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pendingRequests.TryRemove(commandId, out _);
        }
    }

    public Task<JsonElement> PingAsync(CancellationToken cancellationToken = default)
    {
        return RequestAsync("ping", null, cancellationToken);
    }

    public async Task<IHomeAssistantSubscription> SubscribeAsync(
        string commandType,
        IReadOnlyDictionary<string, object?>? payload,
        Func<JsonElement, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandType))
        {
            throw new ArgumentException("A subscription command type is required.", nameof(commandType));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        var registration = new SubscriptionRegistration(
            commandType,
            payload,
            handler,
            _options.SubscriptionBufferCapacity,
            RemoveSubscriptionAsync,
            WriteDiagnostic);
        if (!_subscriptions.TryAdd(registration.Id, registration))
        {
            registration.Dispose();
            throw new HomeAssistantProtocolException("A duplicate local subscription identifier was generated.");
        }

        try
        {
            await ConnectAsync(cancellationToken).ConfigureAwait(false);
            await ActivateSubscriptionAsync(registration, cancellationToken).ConfigureAwait(false);
            return registration;
        }
        catch
        {
            _subscriptions.TryRemove(registration.Id, out _);
            registration.Dispose();
            throw;
        }
    }

    internal Task WaitForSubscriptionCheckpointAsync(
        IHomeAssistantSubscription subscription,
        CancellationToken cancellationToken)
    {
        if (subscription is not SubscriptionRegistration registration)
        {
            throw new ArgumentException("The subscription does not belong to this client.", nameof(subscription));
        }

        return registration.WaitForCheckpointAsync(cancellationToken);
    }

    private async Task AuthenticateAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var greeting = await ReceiveTextAsync(socket, cancellationToken).ConfigureAwait(false);
        using var greetingDocument = JsonDocument.Parse(greeting);
        var greetingType = GetRequiredString(greetingDocument.RootElement, "type");
        if (!string.Equals(greetingType, "auth_required", StringComparison.Ordinal))
        {
            throw new HomeAssistantProtocolException("Unexpected Home Assistant WebSocket greeting: " + greetingType + ".");
        }

        var token = await _options.AccessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new HomeAssistantAuthenticationException("The access token provider returned an empty token.");
        }

        await SendJsonAsync(socket, new Dictionary<string, object?>
        {
            ["type"] = "auth",
            ["access_token"] = token
        }, cancellationToken).ConfigureAwait(false);

        var response = await ReceiveTextAsync(socket, cancellationToken).ConfigureAwait(false);
        using var responseDocument = JsonDocument.Parse(response);
        var responseType = GetRequiredString(responseDocument.RootElement, "type");
        if (string.Equals(responseType, "auth_ok", StringComparison.Ordinal))
        {
            return;
        }

        if (string.Equals(responseType, "auth_invalid", StringComparison.Ordinal))
        {
            throw new HomeAssistantAuthenticationException("Home Assistant rejected the WebSocket access token.");
        }

        throw new HomeAssistantProtocolException("Unexpected Home Assistant authentication response: " + responseType + ".");
    }

    private async Task ActivateSubscriptionAsync(SubscriptionRegistration registration, CancellationToken cancellationToken)
    {
        await registration.LifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (registration.IsStopped)
            {
                return;
            }

            if (registration.ServerId is int activeServerId
                && _serverSubscriptions.TryGetValue(activeServerId, out var activeRegistration)
                && ReferenceEquals(activeRegistration, registration))
            {
                return;
            }

            var serverId = NextCommandId();
            registration.SetServerId(serverId);
            _serverSubscriptions[serverId] = registration;

            var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests[serverId] = completion;
            try
            {
                await SendCommandAsync(serverId, registration.CommandType, registration.Payload, cancellationToken)
                    .ConfigureAwait(false);
                await AwaitWithTimeoutAsync(completion.Task, _options.RequestTimeout, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                _serverSubscriptions.TryRemove(serverId, out _);
                registration.ClearServerId(serverId);
                throw;
            }
            finally
            {
                _pendingRequests.TryRemove(serverId, out _);
            }
        }
        finally
        {
            registration.LifecycleGate.Release();
        }
    }

    private async Task RemoveSubscriptionAsync(SubscriptionRegistration registration, CancellationToken cancellationToken)
    {
        _subscriptions.TryRemove(registration.Id, out _);
        int? serverId;
        await registration.LifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            serverId = registration.ServerId;
            if (serverId is not null)
            {
                registration.ClearServerId(serverId.Value);
                _serverSubscriptions.TryRemove(serverId.Value, out _);
            }
        }
        finally
        {
            registration.LifecycleGate.Release();
        }

        if (serverId is null || _socket?.State != WebSocketState.Open)
        {
            return;
        }

        try
        {
            await RequestAsync(
                "unsubscribe_events",
                new Dictionary<string, object?> { ["subscription"] = serverId.Value },
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            WriteDiagnostic(HomeAssistantDiagnosticLevel.Warning, "websocket.unsubscribe_failed", "A Home Assistant subscription could not be removed cleanly.", ex);
        }
    }

    private async Task SendCommandAsync(
        int commandId,
        string commandType,
        IReadOnlyDictionary<string, object?>? payload,
        CancellationToken cancellationToken)
    {
        var command = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (payload is not null)
        {
            foreach (var item in payload)
            {
                command[item.Key] = item.Value;
            }
        }
        command["id"] = commandId;
        command["type"] = commandType;

        var socket = _socket;
        if (socket is null || socket.State != WebSocketState.Open)
        {
            throw new HomeAssistantConnectionException(
                "The Home Assistant WebSocket is not connected.",
                new WebSocketException(WebSocketError.InvalidState));
        }

        await _sendGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SendJsonAsync(socket, command, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sendGate.Release();
        }
    }

    private static Task SendJsonAsync(ClientWebSocket socket, object payload, CancellationToken cancellationToken)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, HomeAssistantJson.SerializerOptions);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);
    }

    private static async Task<T> AwaitWithTimeoutAsync<T>(Task<T> task, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(timeout, source.Token);
        var completed = await Task.WhenAny(task, delay).ConfigureAwait(false);
        if (completed == task)
        {
            source.Cancel();
            return await task.ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new HomeAssistantConnectionException("The Home Assistant WebSocket command timed out.", new TimeoutException());
    }

    private static HomeAssistantCommandException ReadCommandException(JsonElement root)
    {
        if (!root.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
        {
            return new HomeAssistantCommandException("unknown_error", "Home Assistant rejected the WebSocket command.");
        }

        var code = error.TryGetProperty("code", out var codeProperty) && codeProperty.ValueKind == JsonValueKind.String
            ? codeProperty.GetString() ?? "unknown_error"
            : "unknown_error";
        var message = error.TryGetProperty("message", out var messageProperty) && messageProperty.ValueKind == JsonValueKind.String
            ? messageProperty.GetString() ?? "Home Assistant rejected the WebSocket command."
            : "Home Assistant rejected the WebSocket command.";
        var translationKey = error.TryGetProperty("translation_key", out var translationProperty)
            && translationProperty.ValueKind == JsonValueKind.String
            ? translationProperty.GetString()
            : null;
        return new HomeAssistantCommandException(code, message, translationKey);
    }

    private static string GetRequiredString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new HomeAssistantProtocolException("Home Assistant WebSocket message omitted required property '" + propertyName + "'.");
        }

        return property.GetString() ?? string.Empty;
    }

    private void FailPendingRequests(Exception exception)
    {
        foreach (var completion in _pendingRequests.Values)
        {
            completion.TrySetException(exception);
        }

        _pendingRequests.Clear();
    }

    private int NextCommandId()
    {
        var id = Interlocked.Increment(ref _nextCommandId);
        if (id <= 0)
        {
            Interlocked.Exchange(ref _nextCommandId, 1);
            return 1;
        }

        return id;
    }

    private TimeSpan ApplyJitter(TimeSpan delay)
    {
        if (delay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        var factor = 0.8 + (new Random().NextDouble() * 0.4);
        return TimeSpan.FromMilliseconds(delay.TotalMilliseconds * factor);
    }

    private void SetState(HomeAssistantConnectionState state, Exception? exception = null)
    {
        var previous = _state;
        _state = state;
        if (previous == state && exception is null)
        {
            return;
        }

        var handlers = ConnectionStateChanged;
        if (handlers is null)
        {
            return;
        }

        var args = new HomeAssistantConnectionStateChangedEventArgs(previous, state, exception);
        foreach (EventHandler<HomeAssistantConnectionStateChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(this, args);
            }
            catch (Exception ex)
            {
                WriteDiagnostic(HomeAssistantDiagnosticLevel.Warning, "connection.observer_failed", "A connection-state observer threw an exception.", ex);
            }
        }
    }

    private void WriteDiagnostic(HomeAssistantDiagnosticLevel level, string name, string message, Exception? exception = null)
    {
        try
        {
            _options.Diagnostics.Write(new HomeAssistantDiagnosticEvent(level, name, message, exception));
        }
        catch
        {
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(HomeAssistantWebSocketClient));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _manualDisconnect = true;
        _disposeSource.Cancel();
        _connectionSource?.Cancel();
        _socket?.Dispose();
        _connectionSource = null;
        _socket = null;
        foreach (var registration in _subscriptions.Values)
        {
            registration.Dispose();
        }

        _subscriptions.Clear();
        _serverSubscriptions.Clear();
        FailPendingRequests(new ObjectDisposedException(nameof(HomeAssistantWebSocketClient)));
    }

}
