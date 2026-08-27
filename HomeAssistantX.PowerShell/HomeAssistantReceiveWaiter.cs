using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.PowerShell;

internal static class HomeAssistantReceiveWaiter
{
    internal static async Task WaitAsync(
        IHomeAssistantSubscription subscription,
        Task countReached,
        int? timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var canceled = Task.Delay(Timeout.Infinite, cancellationToken);
        var timeout = timeoutSeconds.HasValue
            ? Task.Delay(TimeSpan.FromSeconds(timeoutSeconds.Value))
            : Task.Delay(Timeout.Infinite);
        var completed = await Task.WhenAny(
            subscription.Completion,
            canceled,
            countReached,
            timeout).ConfigureAwait(false);

        if (completed == countReached || completed == timeout)
        {
            return;
        }

        await completed.ConfigureAwait(false);
    }
}
