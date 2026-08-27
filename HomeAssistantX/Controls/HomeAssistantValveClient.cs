using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

public enum HomeAssistantValveAction
{
    Open,
    Close,
    Stop,
    Toggle
}

/// <summary>Controls water, gas, and other valves through the standard valve domain.</summary>
public sealed class HomeAssistantValveClient : HomeAssistantControlClientBase
{
    internal HomeAssistantValveClient(HomeAssistantServiceClient services) : base(services, "valve") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantValveAction action, CancellationToken cancellationToken = default)
        => CallAsync(action switch
        {
            HomeAssistantValveAction.Open => "open_valve",
            HomeAssistantValveAction.Close => "close_valve",
            HomeAssistantValveAction.Stop => "stop_valve",
            HomeAssistantValveAction.Toggle => "toggle",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported valve action.")
        }, target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetPositionAsync(HomeAssistantTarget target, double positionPercent, CancellationToken cancellationToken = default)
        => CallAsync("set_valve_position", target, call => call.WithData("position", ControlValidation.Percent(positionPercent, nameof(positionPercent))!.Value), cancellationToken);
}
