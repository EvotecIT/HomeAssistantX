using System.Collections.Concurrent;
using System.Text.Json;
using HomeAssistantX.Configuration;
using HomeAssistantX.Diagnostics;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;
using HomeAssistantX.Subscriptions;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.States;

/// <summary>Provides snapshots and reconciled push updates for Home Assistant entity state.</summary>
public sealed class HomeAssistantStateClient : IDisposable
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;
    private readonly HomeAssistantClientOptions _options;
    private readonly ConcurrentDictionary<Guid, LocalStateSubscription> _subscribers = new();
    private readonly Dictionary<string, HomeAssistantState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private readonly object _stateGate = new();
    private IHomeAssistantSubscription? _serverSubscription;
    private Exception? _serverSubscriptionFailure;
    private volatile bool _initialized;
    private bool _snapshotReady;
    private bool _wasConnected;
    private readonly List<HomeAssistantStateChange> _bufferedChanges = new();
    private int _disposed;

    internal HomeAssistantStateClient(
        HomeAssistantRestClient rest,
        HomeAssistantWebSocketClient webSocket,
        HomeAssistantClientOptions options)
    {
        _rest = rest;
        _webSocket = webSocket;
        _options = options;
        _webSocket.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public IReadOnlyDictionary<string, HomeAssistantState> Snapshot
    {
        get
        {
            lock (_stateGate)
            {
                return new Dictionary<string, HomeAssistantState>(_states, StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    public Task<IReadOnlyList<HomeAssistantState>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _rest.GetStatesAsync(cancellationToken);
    }

    public Task<HomeAssistantState> GetAsync(string entityId, CancellationToken cancellationToken = default)
    {
        return _rest.GetStateAsync(entityId, cancellationToken);
    }

    /// <summary>Gets all states through the WebSocket API.</summary>
    public async Task<IReadOnlyList<HomeAssistantState>> GetAllWebSocketAsync(CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.RequestAsync("get_states", null, cancellationToken).ConfigureAwait(false);
        return HomeAssistantJson.DeserializeResponse<HomeAssistantState[]>(result, "The Home Assistant state list could not be decoded.");
    }

    /// <summary>Creates or updates a state representation through REST without controlling the underlying device.</summary>
    public Task<HomeAssistantState> SetAsync(
        string entityId,
        HomeAssistantStateUpdate update,
        CancellationToken cancellationToken = default)
    {
        return _rest.SetStateAsync(entityId, update, cancellationToken);
    }

    /// <summary>Deletes a state representation through REST.</summary>
    public Task<JsonElement> DeleteAsync(string entityId, CancellationToken cancellationToken = default)
    {
        return _rest.DeleteStateAsync(entityId, cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await _initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized)
            {
                return;
            }

            _snapshotReady = false;
            try
            {
                _serverSubscription?.Dispose();
                _serverSubscription = null;
                Volatile.Write(ref _serverSubscriptionFailure, null);
                _serverSubscription = await _webSocket.SubscribeAsync(
                    "subscribe_events",
                    new Dictionary<string, object?> { ["event_type"] = "state_changed" },
                    HandleStateEventAsync,
                    cancellationToken).ConfigureAwait(false);
                _ = ObserveServerSubscriptionAsync(_serverSubscription);
                await ResynchronizeAsync(isReconnect: false, cancellationToken).ConfigureAwait(false);
                _initialized = true;
                _wasConnected = true;
            }
            catch
            {
                _serverSubscription?.Dispose();
                _serverSubscription = null;
                lock (_stateGate)
                {
                    _snapshotReady = false;
                    _bufferedChanges.Clear();
                }

                throw;
            }
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    public async Task<IHomeAssistantSubscription> SubscribeAsync(
        HomeAssistantStateFilter filter,
        Func<HomeAssistantStateChange, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (filter is null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var subscription = new LocalStateSubscription(
            filter,
            handler,
            _options.SubscriptionBufferCapacity,
            RemoveLocalSubscription,
            WriteDiagnostic);
        if (!_subscribers.TryAdd(subscription.Id, subscription))
        {
            subscription.Dispose();
            throw new HomeAssistantProtocolException("A duplicate local state subscription identifier was generated.");
        }

        var serverFailure = Volatile.Read(ref _serverSubscriptionFailure);
        if (serverFailure is not null)
        {
            subscription.Fail(serverFailure);
        }

        return subscription;
    }

    private Task HandleStateEventAsync(JsonElement eventMessage, CancellationToken cancellationToken)
    {
        var eventValue = HomeAssistantJson.DeserializeResponse<HomeAssistantEvent>(eventMessage, "A Home Assistant state event could not be decoded.");
        if (!string.Equals(eventValue.EventType, "state_changed", StringComparison.Ordinal))
        {
            return Task.CompletedTask;
        }

        if (!eventValue.Data.TryGetValue("entity_id", out var entityProperty)
            || entityProperty.ValueKind != JsonValueKind.String)
        {
            throw new HomeAssistantProtocolException("A state_changed event omitted entity_id.");
        }

        var entityId = HomeAssistantEntityId.RequireResponseEntityId(entityProperty.GetString());
        var previous = DeserializeOptionalState(eventValue.Data, "old_state", entityId);
        var current = DeserializeOptionalState(eventValue.Data, "new_state", entityId);
        var change = new HomeAssistantStateChange(entityId, previous, current);

        lock (_stateGate)
        {
            if (!_snapshotReady)
            {
                _bufferedChanges.Add(change);
                return Task.CompletedTask;
            }

            ApplyChange(change);
        }

        Publish(change);
        return Task.CompletedTask;
    }

    private async Task ResynchronizeAsync(bool isReconnect, CancellationToken cancellationToken)
    {
        var snapshot = await _webSocket.RequestAsync("get_states", null, cancellationToken).ConfigureAwait(false);
        if (_serverSubscription is not null)
        {
            // Home Assistant can deliver an event immediately before the snapshot result. Ensure
            // all earlier frames have reached this state reconciler before publishing the snapshot.
            await _webSocket.WaitForSubscriptionCheckpointAsync(_serverSubscription, cancellationToken)
                .ConfigureAwait(false);
        }

        var currentStates = HomeAssistantJson.DeserializeResponse<HomeAssistantState[]>(snapshot, "The get_states response could not be decoded.");
        var changes = new List<HomeAssistantStateChange>();

        lock (_stateGate)
        {
            var previousStates = new Dictionary<string, HomeAssistantState>(_states, StringComparer.OrdinalIgnoreCase);
            _states.Clear();
            foreach (var state in currentStates)
            {
                _states[state.EntityId] = state;
            }

            if (isReconnect)
            {
                foreach (var state in currentStates)
                {
                    previousStates.TryGetValue(state.EntityId, out var previous);
                    if (previous is null || !StatesEquivalent(previous, state))
                    {
                        changes.Add(new HomeAssistantStateChange(state.EntityId, previous, state, true));
                    }

                    previousStates.Remove(state.EntityId);
                }

                foreach (var removed in previousStates)
                {
                    changes.Add(new HomeAssistantStateChange(removed.Key, removed.Value, null, true));
                }
            }

            foreach (var buffered in _bufferedChanges)
            {
                ApplyChange(buffered);
                changes.Add(buffered);
            }

            _bufferedChanges.Clear();
            _snapshotReady = true;
        }

        foreach (var change in changes)
        {
            Publish(change);
        }
    }

    private void OnConnectionStateChanged(object? sender, HomeAssistantConnectionStateChangedEventArgs args)
    {
        if (args.CurrentState == HomeAssistantConnectionState.Reconnecting
            || args.CurrentState == HomeAssistantConnectionState.Disconnected)
        {
            lock (_stateGate)
            {
                _snapshotReady = false;
            }

            return;
        }

        if (args.CurrentState == HomeAssistantConnectionState.Connected && _wasConnected && _initialized)
        {
            _ = ResynchronizeAfterReconnectAsync();
        }

        if (args.CurrentState == HomeAssistantConnectionState.Connected)
        {
            _wasConnected = true;
        }
    }

    private async Task ResynchronizeAfterReconnectAsync()
    {
        try
        {
            await ResynchronizeAsync(isReconnect: true, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            lock (_stateGate)
            {
                _snapshotReady = false;
            }

            WriteDiagnostic(
                HomeAssistantDiagnosticLevel.Error,
                "state.reconciliation_failed",
                "Home Assistant state reconciliation failed after reconnect.",
                ex);
        }
    }

    private void ApplyChange(HomeAssistantStateChange change)
    {
        if (change.CurrentState is null)
        {
            _states.Remove(change.EntityId);
        }
        else
        {
            _states[change.EntityId] = change.CurrentState;
        }
    }

    private void Publish(HomeAssistantStateChange change)
    {
        foreach (var subscriber in _subscribers.Values)
        {
            if (!subscriber.TryPublish(change))
            {
                subscriber.Fail(new HomeAssistantProtocolException(
                    "The state subscription consumer could not keep up with Home Assistant updates."));
            }
        }
    }

    private async Task ObserveServerSubscriptionAsync(IHomeAssistantSubscription subscription)
    {
        Exception? failure = null;
        LocalStateSubscription[] affectedSubscribers;
        try
        {
            await subscription.Completion.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
        }

        if (failure is null)
        {
            return;
        }

        try
        {
            await _initializationGate.WaitAsync().ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return;
        }

        try
        {
            if (!ReferenceEquals(_serverSubscription, subscription))
            {
                return;
            }

            _initialized = false;
            Volatile.Write(ref _serverSubscriptionFailure, failure);
            // Capture the subscribers owned by this failed server subscription while
            // initialization is still blocked. A caller can install a healthy replacement
            // as soon as the gate opens; that replacement must not inherit this stale failure.
            affectedSubscribers = _subscribers.Values.ToArray();
        }
        finally
        {
            _initializationGate.Release();
        }

        foreach (var subscriber in affectedSubscribers)
        {
            subscriber.Fail(failure);
        }
    }

    private void RemoveLocalSubscription(LocalStateSubscription subscription)
    {
        _subscribers.TryRemove(subscription.Id, out _);
    }

    private static HomeAssistantState? DeserializeOptionalState(
        IReadOnlyDictionary<string, JsonElement> data,
        string name,
        string expectedEntityId)
    {
        if (!data.TryGetValue(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        var state = HomeAssistantJson.DeserializeResponse<HomeAssistantState>(value, "A Home Assistant state change could not be decoded.");
        var stateEntityId = HomeAssistantEntityId.RequireResponseEntityId(state.EntityId);
        if (!string.Equals(stateEntityId, expectedEntityId, StringComparison.Ordinal))
        {
            throw new HomeAssistantProtocolException("A Home Assistant state change contained a mismatched entity identifier.");
        }

        return state;
    }

    private static bool StatesEquivalent(HomeAssistantState left, HomeAssistantState right)
    {
        return string.Equals(left.State, right.State, StringComparison.Ordinal)
            && JsonSerializer.Serialize(left.Attributes, HomeAssistantJson.SerializerOptions)
                == JsonSerializer.Serialize(right.Attributes, HomeAssistantJson.SerializerOptions);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(HomeAssistantStateClient));
        }
    }

    private void WriteDiagnostic(
        HomeAssistantDiagnosticLevel level,
        string name,
        string message,
        Exception? exception = null)
    {
        try
        {
            _options.Diagnostics.Write(new HomeAssistantDiagnosticEvent(level, name, message, exception));
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _webSocket.ConnectionStateChanged -= OnConnectionStateChanged;
        _serverSubscription?.Dispose();
        foreach (var subscriber in _subscribers.Values)
        {
            subscriber.Dispose();
        }

        _subscribers.Clear();
        _initializationGate.Dispose();
    }

    private sealed class LocalStateSubscription : IHomeAssistantSubscription
    {
        private readonly HomeAssistantStateFilter _filter;
        private readonly Func<HomeAssistantStateChange, CancellationToken, Task> _handler;
        private readonly Action<LocalStateSubscription> _remove;
        private readonly Action<HomeAssistantDiagnosticLevel, string, string, Exception?> _diagnostic;
        private readonly CancellationTokenSource _source = new();
        private readonly System.Threading.Channels.Channel<HomeAssistantStateChange> _channel;
        private readonly Task _pump;
        private int _stopped;

        public LocalStateSubscription(
            HomeAssistantStateFilter filter,
            Func<HomeAssistantStateChange, CancellationToken, Task> handler,
            int capacity,
            Action<LocalStateSubscription> remove,
            Action<HomeAssistantDiagnosticLevel, string, string, Exception?> diagnostic)
        {
            Id = Guid.NewGuid();
            _filter = filter;
            _handler = handler;
            _remove = remove;
            _diagnostic = diagnostic;
            _channel = System.Threading.Channels.Channel.CreateBounded<HomeAssistantStateChange>(capacity);
            _pump = PumpAsync();
        }

        public Guid Id { get; }

        public Task Completion => _pump;

        public bool TryPublish(HomeAssistantStateChange change)
        {
            return !_filter.Matches(change) || _channel.Writer.TryWrite(change);
        }

        public void Fail(Exception exception)
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
            {
                return;
            }

            _remove(this);
            _diagnostic(
                HomeAssistantDiagnosticLevel.Error,
                "state.subscription_overflow",
                "A state subscription consumer could not keep up with Home Assistant updates.",
                exception);
            _channel.Writer.TryComplete(exception);
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Dispose();
            return Task.CompletedTask;
        }

        private async Task PumpAsync()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(_source.Token).ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var change))
                    {
                        await _handler(change, _source.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (_source.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                if (Interlocked.Exchange(ref _stopped, 1) == 0)
                {
                    _remove(this);
                }

                _diagnostic(
                    HomeAssistantDiagnosticLevel.Error,
                    "state.subscription_handler_failed",
                    "A state subscription handler failed.",
                    ex);
                throw;
            }
            finally
            {
                _source.Dispose();
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
            {
                return;
            }

            _remove(this);
            _channel.Writer.TryComplete();
            try
            {
                _source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The pump can complete between closing the channel and cancellation.
            }
        }
    }
}
