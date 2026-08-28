using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

public abstract class HomeAssistantControlClientBase
{
    private protected HomeAssistantControlClientBase(HomeAssistantServiceClient services, string domain)
    {
        Services = services;
        Domain = domain;
    }

    private protected HomeAssistantServiceClient Services { get; }

    private protected string Domain { get; }

    private protected Task<HomeAssistantServiceCallResult> CallAsync(
        string action,
        HomeAssistantTarget target,
        Action<HomeAssistantServiceCall>? configure,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target is null) throw new ArgumentNullException(nameof(target));
        var call = HomeAssistantServiceCall.Create(Domain, action).ForTarget(target.NormalizeForDomain(Domain, cancellationToken));
        configure?.Invoke(call);
        return Services.CallControlAsync(call, cancellationToken);
    }

    private protected HomeAssistantServiceCallTransport CaptureTransport(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Services.CaptureControlTransport();
    }

    private protected Task<HomeAssistantServiceCallResult> CallAsync(
        string action,
        HomeAssistantTarget target,
        Action<HomeAssistantServiceCall>? configure,
        HomeAssistantServiceCallTransport transport,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target is null) throw new ArgumentNullException(nameof(target));
        var call = HomeAssistantServiceCall.Create(Domain, action).ForTarget(target.NormalizeForDomain(Domain, cancellationToken));
        configure?.Invoke(call);
        return Services.CallControlAsync(call, transport, cancellationToken);
    }
}
