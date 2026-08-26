using System.Management.Automation;
using HomeAssistantX.Notifications;
using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.PowerShell;

/// <summary>Streams persistent-notification changes without polling.</summary>
/// <example><summary>Wait for the current notification snapshot</summary><code>Receive-HomeAssistantNotification -Count 1 -TimeoutSeconds 30</code></example>
[Cmdlet(VerbsCommunications.Receive, "HomeAssistantNotification")]
[OutputType(typeof(HomeAssistantPersistentNotificationUpdate))]
public sealed class ReceiveHomeAssistantNotificationCommand : HomeAssistantCmdlet
{
    /// <summary>Stops after emitting this many updates. Omit it to keep streaming.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? Count { get; set; }

    /// <summary>Stops normally after this many seconds.</summary>
    [Parameter]
    [ValidateRange(1, 86400)]
    public int? TimeoutSeconds { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var streams = CapturePipelineStreams();
        var received = 0;
        var countReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IHomeAssistantSubscription? subscription = null;
        try
        {
            subscription = await Client.Notifications.SubscribePersistentAsync((update, _) =>
            {
                var number = Interlocked.Increment(ref received);
                if (!Count.HasValue || number <= Count.Value) streams.WriteObject(update);
                if (Count.HasValue && number >= Count.Value) countReached.TrySetResult(true);
                return Task.CompletedTask;
            }, CancelToken).ConfigureAwait(false);
            var canceled = Task.Delay(Timeout.Infinite, CancelToken);
            var timeout = TimeoutSeconds.HasValue ? Task.Delay(TimeSpan.FromSeconds(TimeoutSeconds.Value), CancelToken) : Task.Delay(Timeout.Infinite, CancelToken);
            var completed = await Task.WhenAny(subscription.Completion, canceled, countReached.Task, timeout).ConfigureAwait(false);
            if (completed != countReached.Task && completed != timeout) await completed.ConfigureAwait(false);
        }
        finally
        {
            if (subscription is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await subscription.StopAsync(cleanup.Token).ConfigureAwait(false); }
                catch (Exception ex) when (ex is OperationCanceledException || ex is Exceptions.HomeAssistantException || ex is ObjectDisposedException) { }
                subscription.Dispose();
            }
        }
    }
}
