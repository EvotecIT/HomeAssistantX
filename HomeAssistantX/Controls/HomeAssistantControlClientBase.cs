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
        var call = HomeAssistantServiceCall.Create(Domain, action).ForTarget(target);
        configure?.Invoke(call);
        return Services.CallAsync(call, cancellationToken);
    }
}
