using System.Text.Json;
using System.Threading.Channels;
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
        private readonly object _progressGate = new();
        private readonly Task _pump;
        private TaskCompletionSource<bool> _progress = CreateProgressSource();
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

        public bool IsStopped => Volatile.Read(ref _stopped) != 0;

        public Task Completion => _pump;

        public void SetServerId(int serverId)
        {
            ServerId = serverId;
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
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
            {
                return;
            }

            _channel.Writer.TryComplete(exception);
            await _stop(this, CancellationToken.None).ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
            {
                return;
            }

            await _stop(this, cancellationToken).ConfigureAwait(false);
            _channel.Writer.TryComplete();
            _source.Cancel();
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
            }
            catch (Exception ex)
            {
                _diagnostic(HomeAssistantDiagnosticLevel.Error, "subscription.handler_failed", "A Home Assistant subscription handler failed.", ex);
                if (Interlocked.Exchange(ref _stopped, 1) == 0)
                {
                    await _stop(this, CancellationToken.None).ConfigureAwait(false);
                }

                throw;
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
            if (Interlocked.Exchange(ref _stopped, 1) == 0)
            {
                _ = _stop(this, CancellationToken.None);
            }

            _channel.Writer.TryComplete();
            _source.Cancel();
        }
    }
}
