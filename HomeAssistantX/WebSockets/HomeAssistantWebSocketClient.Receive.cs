using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HomeAssistantX.Diagnostics;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.WebSockets;

public sealed partial class HomeAssistantWebSocketClient
{
    private async Task ReceiveLoopAsync(
        ClientWebSocket socket,
        CancellationTokenSource connectionSource)
    {
        var cancellationToken = connectionSource.Token;
        Exception? failure = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var message = await ReceiveTextAsync(socket, cancellationToken).ConfigureAwait(false);
                RouteMessage(message);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            failure = ex;
            WriteDiagnostic(HomeAssistantDiagnosticLevel.Warning, "websocket.disconnected", "The Home Assistant WebSocket connection ended unexpectedly.", ex);
        }
        finally
        {
            var ownsConnection = ReferenceEquals(_socket, socket)
                || ReferenceEquals(_connectionSource, connectionSource);
            if (ReferenceEquals(_socket, socket))
            {
                _socket = null;
            }

            if (ReferenceEquals(_connectionSource, connectionSource))
            {
                _connectionSource = null;
            }

            socket.Dispose();
            connectionSource.Dispose();
            if (ownsConnection)
            {
                _serverSubscriptions.Clear();
                FailPendingRequests(new HomeAssistantConnectionException(
                    "The Home Assistant WebSocket connection ended before the command completed.",
                    failure ?? new WebSocketException(WebSocketError.ConnectionClosedPrematurely)));

                if (!_manualDisconnect && !_disposeSource.IsCancellationRequested && !_subscriptions.IsEmpty)
                {
                    SetState(HomeAssistantConnectionState.Reconnecting, failure);
                    StartReconnectLoop();
                }
                else
                {
                    SetState(HomeAssistantConnectionState.Disconnected, failure);
                }
            }
        }
    }

    private void RouteMessage(string message)
    {
        using var document = HomeAssistantJson.ParseResponse(
            message,
            "A Home Assistant WebSocket message could not be decoded.");
        var root = document.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            RouteMessage(root);
            return;
        }

        if (root.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantProtocolException(
                "Home Assistant returned a WebSocket payload that was neither a message nor a coalesced batch.");
        }

        var messageCount = root.GetArrayLength();
        if (messageCount > _options.MaximumCoalescedWebSocketMessages)
        {
            throw new HomeAssistantProtocolException(
                "A Home Assistant coalesced WebSocket batch exceeded the configured message-count limit.");
        }

        foreach (var item in root.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw new HomeAssistantProtocolException(
                    "A Home Assistant coalesced WebSocket batch contained a non-message value.");
            }

            _ = GetRequiredString(item, "type");
        }

        foreach (var item in root.EnumerateArray())
        {
            RouteMessage(item);
        }
    }

    private void RouteMessage(JsonElement root)
    {
        var type = GetRequiredString(root, "type");
        if (!root.TryGetProperty("id", out var idProperty) || !idProperty.TryGetInt32(out var id))
        {
            if (!string.Equals(type, "pong", StringComparison.Ordinal))
            {
                WriteDiagnostic(HomeAssistantDiagnosticLevel.Trace, "websocket.unsolicited", "Received a Home Assistant message without a command identifier.");
            }

            return;
        }

        if (string.Equals(type, "result", StringComparison.Ordinal))
        {
            if (!_pendingRequests.TryGetValue(id, out var completion))
            {
                return;
            }

            var success = root.TryGetProperty("success", out var successProperty) && successProperty.ValueKind == JsonValueKind.True;
            if (success)
            {
                var result = root.TryGetProperty("result", out var resultProperty)
                    ? resultProperty.Clone()
                    : JsonDocument.Parse("null").RootElement.Clone();
                completion.TrySetResult(result);
            }
            else
            {
                completion.TrySetException(ReadCommandException(root));
            }

            return;
        }

        if (string.Equals(type, "pong", StringComparison.Ordinal))
        {
            if (_pendingRequests.TryGetValue(id, out var pongCompletion))
            {
                pongCompletion.TrySetResult(JsonDocument.Parse("null").RootElement.Clone());
            }

            return;
        }

        if (string.Equals(type, "event", StringComparison.Ordinal)
            && _serverSubscriptions.TryGetValue(id, out var registration)
            && root.TryGetProperty("event", out var eventProperty))
        {
            if (!registration.TryPublish(eventProperty.Clone()))
            {
                _ = registration.FailAndStopAsync(new HomeAssistantProtocolException(
                    "The subscription consumer could not keep up with Home Assistant events."));
            }
        }
    }

    private void StartReconnectLoop()
    {
        if (_reconnectTask is { IsCompleted: false })
        {
            return;
        }

        _reconnectTask = Task.Run(ReconnectLoopAsync);
    }

    private async Task ReconnectLoopAsync()
    {
        var delay = _options.ReconnectMinimumDelay;
        while (!_disposeSource.IsCancellationRequested && !_manualDisconnect && !_subscriptions.IsEmpty)
        {
            try
            {
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(ApplyJitter(delay), _disposeSource.Token).ConfigureAwait(false);
                }

                await ConnectAsync(_disposeSource.Token, stopOnPermanentNegotiationFailure: true).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException) when (_disposeSource.IsCancellationRequested)
            {
                return;
            }
            catch (HomeAssistantAuthenticationException ex)
            {
                SetState(HomeAssistantConnectionState.Faulted, ex);
                WriteDiagnostic(
                    HomeAssistantDiagnosticLevel.Error,
                    "websocket.reconnect_authentication_failed",
                    "Home Assistant rejected the recovered WebSocket credentials; automatic reconnect stopped.",
                    ex);
                await FailSubscriptionsAsync(ex).ConfigureAwait(false);
                return;
            }
            catch (PermanentReconnectNegotiationException ex)
            {
                SetState(HomeAssistantConnectionState.Faulted, ex.Failure);
                WriteDiagnostic(
                    HomeAssistantDiagnosticLevel.Error,
                    "websocket.reconnect_permanent_failure",
                    "Home Assistant rejected WebSocket negotiation or returned an invalid protocol response; automatic reconnect stopped.",
                    ex.Failure);
                await FailSubscriptionsAsync(ex.Failure).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                WriteDiagnostic(HomeAssistantDiagnosticLevel.Warning, "websocket.reconnect_failed", "Home Assistant WebSocket reconnect failed; another attempt will follow.", ex);
                var doubledTicks = delay.Ticks > long.MaxValue / 2 ? long.MaxValue : delay.Ticks * 2;
                delay = TimeSpan.FromTicks(Math.Min(doubledTicks, _options.ReconnectMaximumDelay.Ticks));
            }
        }
    }

    private async Task FailSubscriptionsAsync(Exception failure)
    {
        var registrations = _subscriptions.Values.ToArray();
        await Task.WhenAll(registrations.Select(registration => registration.FailAndStopAsync(failure)))
            .ConfigureAwait(false);
    }

    private async Task<string> ReceiveTextAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var stream = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException(WebSocketError.ConnectionClosedPrematurely);
            }

            if (result.MessageType != WebSocketMessageType.Text)
            {
                throw new HomeAssistantProtocolException("Home Assistant returned a non-text WebSocket message.");
            }

            if (stream.Length + result.Count > _options.MaximumWebSocketMessageBytes)
            {
                throw new HomeAssistantProtocolException("A Home Assistant WebSocket message exceeded the configured size limit.");
            }

            stream.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
    }

    private sealed class PermanentReconnectNegotiationException : Exception
    {
        internal PermanentReconnectNegotiationException(HomeAssistantException failure)
            : base(failure.Message, failure)
        {
            Failure = failure;
        }

        internal HomeAssistantException Failure { get; }
    }
}
