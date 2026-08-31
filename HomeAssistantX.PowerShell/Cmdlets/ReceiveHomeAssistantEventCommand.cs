using System.Management.Automation;
using HomeAssistantX.Models;
using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.PowerShell;

/// <summary>Streams Home Assistant events without polling until canceled.</summary>
/// <example>
///   <summary>Wait for one matching state change</summary>
///   <code>$event = $ha | Receive-HomeAssistantEvent -EntityId 'binary_sensor.front_door' -Count 1 -TimeoutSeconds 60</code>
///   <para>Uses a WebSocket subscription and returns after one matching event or the timeout.</para>
/// </example>
[Cmdlet(VerbsCommunications.Receive, "HomeAssistantEvent", DefaultParameterSetName = EventParameterSet)]
[OutputType(typeof(HomeAssistantEvent))]
public sealed class ReceiveHomeAssistantEventCommand : HomeAssistantCmdlet
{
    private const string EventParameterSet = "Event";
    private const string EntityParameterSet = "Entity";
    private const string AllParameterSet = "All";

    /// <summary>Exact Home Assistant event type to stream.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = EventParameterSet)]
    [ValidateNotNullOrEmpty]
    public string EventType { get; set; } = string.Empty;

    /// <summary>Streams state-change events only for these entity identifiers.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = EntityParameterSet)]
    [ValidateNotNullOrEmpty]
    public string[] EntityId { get; set; } = Array.Empty<string>();

    /// <summary>Streams all Home Assistant event types until the pipeline is stopped.</summary>
    [Parameter(Mandatory = true, ParameterSetName = AllParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter All { get; set; }

    /// <summary>Stops after emitting this many matching events. Omit it to keep streaming.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? Count { get; set; }

    /// <summary>Stops normally when this many seconds elapse without requiring pipeline cancellation.</summary>
    [Parameter]
    [ValidateRange(1, 86400)]
    public int? TimeoutSeconds { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var streams = CapturePipelineStreams();
        var filter = ParameterSetName == EntityParameterSet
            ? new HashSet<string>(NormalizeEntityIds(EntityId), StringComparer.Ordinal)
            : null;
        var eventType = ParameterSetName switch
        {
            EntityParameterSet => "state_changed",
            AllParameterSet => null,
            _ => EventType
        };
        var received = 0;
        var countReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        IHomeAssistantSubscription? subscription = null;
        try
        {
            subscription = await Client.Events.SubscribeAsync(
                eventType,
                (value, _) =>
                {
                    if (filter is null || IsMatchingEntity(value, filter))
                    {
                        var eventNumber = Interlocked.Increment(ref received);
                        if (!Count.HasValue || eventNumber <= Count.Value)
                        {
                            streams.WriteObject(value);
                        }

                        if (Count.HasValue && eventNumber >= Count.Value)
                        {
                            countReached.TrySetResult(true);
                        }
                    }

                    return Task.CompletedTask;
                },
                CancelToken).ConfigureAwait(false);
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
                try
                {
                    await subscription.StopAsync(cleanup.Token).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is OperationCanceledException
                    || ex is Exceptions.HomeAssistantException
                    || ex is ObjectDisposedException)
                {
                }

                subscription.Dispose();
            }
        }
    }

    private static bool IsMatchingEntity(HomeAssistantEvent value, ISet<string> filter)
    {
        return value.Data.TryGetValue("entity_id", out var entityId)
            && entityId.ValueKind == System.Text.Json.JsonValueKind.String
            && filter.Contains(entityId.GetString() ?? string.Empty);
    }

    private static IEnumerable<string> NormalizeEntityIds(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (!HomeAssistantEntityId.TryNormalize(value, out var normalized))
            {
                throw new ArgumentException(
                    "EntityId must contain lowercase native Home Assistant entity identifiers.",
                    nameof(EntityId));
            }

            yield return normalized;
        }
    }
}
