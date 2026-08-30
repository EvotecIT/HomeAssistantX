using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

public enum HomeAssistantFanAction
{
    TurnOn,
    TurnOff,
    Toggle,
    IncreaseSpeed,
    DecreaseSpeed
}

public enum HomeAssistantFanDirection
{
    Forward,
    Reverse
}

/// <summary>Controls fans using the standard Home Assistant fan service contract.</summary>
public sealed class HomeAssistantFanClient : HomeAssistantControlClientBase
{
    internal HomeAssistantFanClient(HomeAssistantServiceClient services) : base(services, "fan") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantFanAction action, int? percentageStep = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var service = action switch
        {
            HomeAssistantFanAction.TurnOn => "turn_on",
            HomeAssistantFanAction.TurnOff => "turn_off",
            HomeAssistantFanAction.Toggle => "toggle",
            HomeAssistantFanAction.IncreaseSpeed => "increase_speed",
            HomeAssistantFanAction.DecreaseSpeed => "decrease_speed",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported fan action.")
        };
        if (percentageStep.HasValue && action != HomeAssistantFanAction.IncreaseSpeed && action != HomeAssistantFanAction.DecreaseSpeed)
        {
            throw new ArgumentException("A percentage step is valid only when increasing or decreasing speed.", nameof(percentageStep));
        }

        return CallAsync(service, target, call =>
        {
            if (percentageStep.HasValue)
            {
                call.WithData("percentage_step", ControlValidation.PercentInt(percentageStep.Value, nameof(percentageStep)));
            }
        }, cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetPercentageAsync(HomeAssistantTarget target, int percentage, CancellationToken cancellationToken = default)
        => CallAsync("set_percentage", target, call => call.WithData("percentage", ControlValidation.PercentInt(percentage, nameof(percentage))), cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetOscillationAsync(HomeAssistantTarget target, bool oscillating, CancellationToken cancellationToken = default)
        => CallAsync("oscillate", target, call => call.WithData("oscillating", oscillating), cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetDirectionAsync(HomeAssistantTarget target, HomeAssistantFanDirection direction, CancellationToken cancellationToken = default)
        => CallAsync("set_direction", target, call => call.WithData("direction", direction switch
        {
            HomeAssistantFanDirection.Forward => "forward",
            HomeAssistantFanDirection.Reverse => "reverse",
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unsupported fan direction.")
        }), cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetPresetModeAsync(HomeAssistantTarget target, string presetMode, CancellationToken cancellationToken = default)
        => CallAsync("set_preset_mode", target, call => call.WithData("preset_mode", ControlValidation.RequiredUnchanged(presetMode, nameof(presetMode), cancellationToken)), cancellationToken);
}
