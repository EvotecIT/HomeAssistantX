using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

/// <summary>Invokes standard Home Assistant cover actions with validated positions.</summary>
public sealed class HomeAssistantCoverClient : HomeAssistantControlClientBase
{
    internal HomeAssistantCoverClient(HomeAssistantServiceClient services) : base(services, "cover") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantCoverAction action, CancellationToken cancellationToken = default)
        => CallAsync(action switch
        {
            HomeAssistantCoverAction.Open => "open_cover",
            HomeAssistantCoverAction.Close => "close_cover",
            HomeAssistantCoverAction.Stop => "stop_cover",
            HomeAssistantCoverAction.Toggle => "toggle",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported cover action.")
        }, target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetPositionAsync(HomeAssistantTarget target, double positionPercent, CancellationToken cancellationToken = default)
        => CallAsync("set_cover_position", target, call => call.WithData("position", ControlValidation.Percent(positionPercent, nameof(positionPercent))!.Value), cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetTiltPositionAsync(HomeAssistantTarget target, double positionPercent, CancellationToken cancellationToken = default)
        => CallAsync("set_cover_tilt_position", target, call => call.WithData("tilt_position", ControlValidation.Percent(positionPercent, nameof(positionPercent))!.Value), cancellationToken);
}
