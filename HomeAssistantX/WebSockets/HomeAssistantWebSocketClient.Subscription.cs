using System.Text.Json;
using System.Threading.Channels;
using System.Net.WebSockets;
using HomeAssistantX.Diagnostics;
using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.WebSockets;

public sealed partial class HomeAssistantWebSocketClient
{
    private sealed class SubscriptionRegistration : IHomeAssistantSubscription
    {
        private readonly Channel<JsonElement> _channel;
        private readonly Func<JsonElement, CancellationToken, Task> _handler;
        private readonly Func<SubscriptionRegistration, CancellationToken, Task> _stop;
        private readonly Action<HomeAssistantDiagnosticLevel, string, string, Exception?> _diagnostic;
        private readonly CancellationTokenSource _source = new();
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
        private readonly object _progressGate = new();
        private readonly object _stopGate = new();
        private readonly Task _pump;
        private TaskCompletionSource<bool> _progress = CreateProgressSource();
        private Task? _stopTask;
        private Exception? _terminalFailure;
        private long _publishedSequence;
        private long _processedSequence;
        private int _stopped;

        public SubscriptionRegistration(
            string commandType,
            IReadOnlyDictionary<string, object?>? payload,
            Func<JsonElement, CancellationToken, Task> handler,
            int capacity,
            Func<SubscriptionRegistration, CancellationToken, Task> stop,
            Action<HomeAssistantDiagnosticLevel, string, string, Exception?> diagnostic)
        {
            Id = Guid.NewGuid();
            CommandType = commandType;
            Payload = payload;
            _handler = handler;
            _stop = stop;
            _diagnostic = diagnostic;
            _channel = Channel.CreateBounded<JsonElement>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
            _pump = PumpAsync();
        }

        public Guid Id { get; }

        public string CommandType { get; }

        public IReadOnlyDictionary<string, object?>? Payload { get; }

        public int? ServerId { get; private set; }

        public ClientWebSocket? ServerSocket { get; private set; }

        public SemaphoreSlim LifecycleGate => _lifecycleGate;

        public bool IsStopped => Volatile.Read(ref _stopped) != 0;

        public Task Completion => _pump;

        public void SetServerSubscription(int serverId, ClientWebSocket socket)
        {
            ServerId = serverId;
            ServerSocket = socket;
        }

        public void ClearServerSubscription(int expectedServerId)
        {
            if (ServerId == expectedServerId)
            {
                ServerId = null;
                ServerSocket = null;
            }
        }

        public bool TryPublish(JsonElement message)
        {
            if (IsStopped || !_channel.Writer.TryWrite(message))
            {
                return false;
            }

            Interlocked.Increment(ref _publishedSequence);
            return true;
        }

        public async Task FailAndStopAsync(Exception exception)
        {
            await EnsureStopStarted(exception).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await AwaitWithCancellationAsync(
                EnsureStopStarted(exception: null),
                cancellationToken).ConfigureAwait(false);
        }

        private async Task PumpAsync()
        {
            try
            {
                while (await _channel.Reader.WaitToReadAsync(_source.Token).ConfigureAwait(false))
                {
                    while (_channel.Reader.TryRead(out var message))
                    {
                        try
                        {
                            await _handler(message, _source.Token).ConfigureAwait(false);
                        }
                        catch (Exception) when (_source.IsCancellationRequested)
                        {
                            // A terminal upstream failure cancels an already-running handler so it
                            // cannot keep Completion pending. Discarding buffered events lets the
                            // reader completion settle while preserving the upstream exception.
                            while (_channel.Reader.TryRead(out _))
                            {
                                Interlocked.Increment(ref _processedSequence);
                                SignalProgress();
                            }

                            await _channel.Reader.Completion.ConfigureAwait(false);
                            RethrowTerminalFailure();
                            return;
                        }
                        catch (Exception ex)
                        {
                            _diagnostic(HomeAssistantDiagnosticLevel.Error, "subscription.handler_failed", "A Home Assistant subscription handler failed.", ex);
                            await EnsureStopStarted(ex).ConfigureAwait(false);
                            throw;
                        }
                        finally
                        {
                            Interlocked.Increment(ref _processedSequence);
                            SignalProgress();
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (_source.IsCancellationRequested)
            {
                RethrowTerminalFailure();
            }
            finally
            {
                _source.Dispose();
            }
        }

        public async Task WaitForCheckpointAsync(CancellationToken cancellationToken)
        {
            var target = Interlocked.Read(ref _publishedSequence);
            while (true)
            {
                Task progress;
                lock (_progressGate)
                {
                    if (Interlocked.Read(ref _processedSequence) >= target)
                    {
                        return;
                    }

                    progress = _progress.Task;
                }

                if (!cancellationToken.CanBeCanceled)
                {
                    await progress.ConfigureAwait(false);
                    continue;
                }

                var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using (cancellationToken.Register(() => canceled.TrySetCanceled()))
                {
                    var completed = await Task.WhenAny(progress, canceled.Task).ConfigureAwait(false);
                    await completed.ConfigureAwait(false);
                }
            }
        }

        private void SignalProgress()
        {
            TaskCompletionSource<bool> progress;
            lock (_progressGate)
            {
                progress = _progress;
                _progress = CreateProgressSource();
            }

            progress.TrySetResult(true);
        }

        private static TaskCompletionSource<bool> CreateProgressSource()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public void Dispose()
        {
            _ = EnsureStopStarted(exception: null);
        }

        private Task EnsureStopStarted(Exception? exception)
        {
            lock (_stopGate)
            {
                if (_stopTask is not null)
                {
                    return _stopTask;
                }

                _ = Interlocked.Exchange(ref _stopped, 1);
                if (exception is null)
                {
                    _channel.Writer.TryComplete();
                    CancelSource();
                }
                else
                {
                    Volatile.Write(ref _terminalFailure, exception);
                    _channel.Writer.TryComplete(exception);
                    CancelSource();
                }

                _stopTask = _stop(this, CancellationToken.None);
                return _stopTask;
            }
        }

        private static async Task AwaitWithCancellationAsync(Task task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }

            var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => canceled.TrySetCanceled()))
            {
                var completed = await Task.WhenAny(task, canceled.Task).ConfigureAwait(false);
                await completed.ConfigureAwait(false);
            }
        }

        private void CancelSource()
        {
            try
            {
                _source.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The pump can finish and dispose its token source while an unsubscribe is in flight.
            }
        }

        private void RethrowTerminalFailure()
        {
            var terminalFailure = Volatile.Read(ref _terminalFailure);
            if (terminalFailure is not null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(terminalFailure).Throw();
            }
        }
    }
}
