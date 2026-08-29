using HomeAssistantX.Services;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Controls;

public enum HomeAssistantVacuumAction
{
    Start,
    Pause,
    Stop,
    ReturnToBase,
    Locate,
    CleanSpot
}

public enum HomeAssistantLawnMowerAction
{
    StartMowing,
    Pause,
    Dock
}

/// <summary>Controls the portable service contract shared by Home Assistant vacuum entities.</summary>
public sealed class HomeAssistantVacuumClient : HomeAssistantControlClientBase
{
    internal HomeAssistantVacuumClient(HomeAssistantServiceClient services) : base(services, "vacuum") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantVacuumAction action, CancellationToken cancellationToken = default)
        => CallAsync(action switch
        {
            HomeAssistantVacuumAction.Start => "start",
            HomeAssistantVacuumAction.Pause => "pause",
            HomeAssistantVacuumAction.Stop => "stop",
            HomeAssistantVacuumAction.ReturnToBase => "return_to_base",
            HomeAssistantVacuumAction.Locate => "locate",
            HomeAssistantVacuumAction.CleanSpot => "clean_spot",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported vacuum action.")
        }, target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetFanSpeedAsync(HomeAssistantTarget target, string fanSpeed, CancellationToken cancellationToken = default)
        => CallAsync("set_fan_speed", target, call => call.WithData("fan_speed", ControlValidation.RequiredUnchanged(fanSpeed, nameof(fanSpeed))), cancellationToken);

    public Task<HomeAssistantServiceCallResult> CleanAreaAsync(HomeAssistantTarget target, IReadOnlyList<string> areaIds, CancellationToken cancellationToken = default)
        => CallAsync("clean_area", target, call => call.WithData(
            "cleaning_area_id",
            ControlValidation.RequiredValuesUnchanged(areaIds, nameof(areaIds), cancellationToken)), cancellationToken);

    /// <summary>Sends a provider-specific vacuum command while keeping the common target contract typed.</summary>
    public Task<HomeAssistantServiceCallResult> SendCommandAsync(HomeAssistantTarget target, string command, object? parameters = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedCommand = ControlValidation.RequiredUnchanged(command, nameof(command), cancellationToken);
        var frozenParameters = parameters is null ? (System.Text.Json.JsonElement?)null : HomeAssistantJson.FreezeValue(parameters, nameof(parameters), "Parameters", cancellationToken);
        if (frozenParameters.HasValue && frozenParameters.Value.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new ArgumentException("Vacuum command parameters must serialize as a JSON object.", nameof(parameters));
        return CallAsync("send_command", target, call =>
        {
            call.WithData("command", normalizedCommand);
            if (frozenParameters.HasValue)
            {
                call.WithData("params", frozenParameters.Value);
            }
        }, cancellationToken);
    }
}

/// <summary>Controls the standard Home Assistant lawn mower lifecycle.</summary>
public sealed class HomeAssistantLawnMowerClient : HomeAssistantControlClientBase
{
    internal HomeAssistantLawnMowerClient(HomeAssistantServiceClient services) : base(services, "lawn_mower") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantLawnMowerAction action, CancellationToken cancellationToken = default)
        => CallAsync(action switch
        {
            HomeAssistantLawnMowerAction.StartMowing => "start_mowing",
            HomeAssistantLawnMowerAction.Pause => "pause",
            HomeAssistantLawnMowerAction.Dock => "dock",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported lawn mower action.")
        }, target, null, cancellationToken);
}
