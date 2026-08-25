namespace HomeAssistantX.Subscriptions;

/// <summary>Controls the lifetime of a server-side Home Assistant subscription.</summary>
public interface IHomeAssistantSubscription : IDisposable
{
    Guid Id { get; }

    Task Completion { get; }

    Task StopAsync(CancellationToken cancellationToken = default);
}
