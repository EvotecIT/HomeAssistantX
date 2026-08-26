using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

/// <summary>Invokes the standard Home Assistant switch actions.</summary>
public sealed class HomeAssistantSwitchClient : HomeAssistantControlClientBase
{
    internal HomeAssistantSwitchClient(HomeAssistantServiceClient services) : base(services, "switch") { }

    public Task<HomeAssistantServiceCallResult> TurnOnAsync(HomeAssistantTarget target, CancellationToken cancellationToken = default)
        => CallAsync("turn_on", target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> TurnOffAsync(HomeAssistantTarget target, CancellationToken cancellationToken = default)
        => CallAsync("turn_off", target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> ToggleAsync(HomeAssistantTarget target, CancellationToken cancellationToken = default)
        => CallAsync("toggle", target, null, cancellationToken);
}
