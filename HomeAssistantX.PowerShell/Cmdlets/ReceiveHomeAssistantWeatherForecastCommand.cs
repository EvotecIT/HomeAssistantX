using System.Management.Automation;
using HomeAssistantX.Subscriptions;
using HomeAssistantX.Weather;

namespace HomeAssistantX.PowerShell;

/// <summary>Streams weather forecast updates without polling.</summary>
/// <example><summary>Wait for the next hourly forecast</summary><code>Receive-HomeAssistantWeatherForecast weather.home -ForecastType Hourly -Count 1 -TimeoutSeconds 30</code></example>
[Cmdlet(VerbsCommunications.Receive, "HomeAssistantWeatherForecast")]
[OutputType(typeof(HomeAssistantWeatherForecastUpdate))]
public sealed class ReceiveHomeAssistantWeatherForecastCommand : HomeAssistantCmdlet
{
    [Parameter(Mandatory = true, Position = 0)][ValidateNotNullOrEmpty] public string EntityId { get; set; } = string.Empty;
    [Parameter(Mandatory = true)] public HomeAssistantWeatherForecastType ForecastType { get; set; }
    [Parameter][ValidateRange(1, int.MaxValue)] public int? Count { get; set; }
    [Parameter][ValidateRange(1, 86400)] public int? TimeoutSeconds { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var streams = CapturePipelineStreams();
        var received = 0;
        var countReached = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        IHomeAssistantSubscription? subscription = null;
        try
        {
            subscription = await Client.Weather.SubscribeForecastAsync(EntityId, ForecastType, (update, _) =>
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
