using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

/// <summary>Invokes the standard Home Assistant light actions with validated typed values.</summary>
public sealed class HomeAssistantLightClient : HomeAssistantControlClientBase
{
    internal HomeAssistantLightClient(HomeAssistantServiceClient services) : base(services, "light") { }

    public Task<HomeAssistantServiceCallResult> TurnOnAsync(
        HomeAssistantTarget target,
        HomeAssistantLightOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var frozenOptions = options?.Snapshot(cancellationToken);
        return CallAsync(
            "turn_on",
            target,
            frozenOptions is null ? null : call => frozenOptions.Apply(call),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> TurnOffAsync(HomeAssistantTarget target, TimeSpan? transition = null, CancellationToken cancellationToken = default)
        => CallAsync("turn_off", target, transition.HasValue ? call => call.WithData("transition", ControlValidation.Duration(transition, nameof(transition), TimeSpan.FromSeconds(6553))!.Value.TotalSeconds) : null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> ToggleAsync(
        HomeAssistantTarget target,
        HomeAssistantLightOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var frozenOptions = options?.Snapshot(cancellationToken);
        return CallAsync(
            "toggle",
            target,
            frozenOptions is null ? null : call => frozenOptions.Apply(call),
            cancellationToken);
    }
}
