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
        using var waitSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeout = timeoutSeconds.HasValue
            ? Task.Delay(TimeSpan.FromSeconds(timeoutSeconds.Value), waitSource.Token)
            : Task.Delay(Timeout.Infinite, waitSource.Token);
        try
        {
            var completed = await Task.WhenAny(subscription.Completion, countReached, timeout).ConfigureAwait(false);

            if (completed == countReached)
            {
                return;
            }

            if (completed == timeout)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            await completed.ConfigureAwait(false);
        }
        finally
        {
            waitSource.Cancel();
        }
    }
}
