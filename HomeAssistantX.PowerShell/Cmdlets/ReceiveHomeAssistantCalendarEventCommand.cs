using System.Management.Automation;
using HomeAssistantX.Calendars;
using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.PowerShell;

/// <summary>Streams refreshed event lists for one calendar without polling.</summary>
/// <example><summary>Wait for one event-list snapshot</summary><code>Receive-HomeAssistantCalendarEvent -EntityId calendar.home -Count 1 -TimeoutSeconds 30</code></example>
[Cmdlet(VerbsCommunications.Receive, "HomeAssistantCalendarEvent")]
[OutputType(typeof(HomeAssistantCalendarEventUpdate))]
public sealed class ReceiveHomeAssistantCalendarEventCommand : HomeAssistantCmdlet
{
    /// <summary>Calendar entity identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Range start. Defaults to the current instant.</summary>
    [Parameter]
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>Range end. Defaults to 30 days after the start.</summary>
    [Parameter]
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>Stops after emitting this many event-list updates.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? Count { get; set; }

    /// <summary>Stops normally after this many seconds.</summary>
    [Parameter]
    [ValidateRange(1, 86400)]
    public int? TimeoutSeconds { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var start = StartTime ?? DateTimeOffset.Now;
        var end = EndTime ?? start.AddDays(30);
        var streams = CapturePipelineStreams();
        var received = 0;
        var countReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IHomeAssistantSubscription? subscription = null;
        try
        {
            subscription = await Client.Calendars.SubscribeAsync(EntityId, start, end, (update, _) =>
            {
                var number = Interlocked.Increment(ref received);
                if (!Count.HasValue || number <= Count.Value) streams.WriteObject(update);
                if (Count.HasValue && number >= Count.Value) countReached.TrySetResult(true);
                return Task.CompletedTask;
            }, CancelToken).ConfigureAwait(false);
            await HomeAssistantReceiveWaiter.WaitAsync(
                subscription,
                countReached.Task,
                TimeoutSeconds,
                CancelToken).ConfigureAwait(false);
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
