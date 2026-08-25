namespace HomeAssistantX.Subscriptions;

/// <summary>Controls the lifetime of a server-side Home Assistant subscription.</summary>
public interface IHomeAssistantSubscription : IDisposable
{
    Guid Id { get; }

    Task Completion { get; }

    /// <summary>
    /// Stops the subscription. Cancellation is honored before cleanup begins; once begun, cleanup completes atomically.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}
