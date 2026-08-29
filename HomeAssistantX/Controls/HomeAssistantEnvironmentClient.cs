using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

public enum HomeAssistantHumidifierAction
{
    TurnOn,
    TurnOff,
    Toggle
}

public enum HomeAssistantWaterHeaterAction
{
    TurnOn,
    TurnOff
}

/// <summary>Controls humidifier power, target humidity, and modes.</summary>
public sealed class HomeAssistantHumidifierClient : HomeAssistantControlClientBase
{
    internal HomeAssistantHumidifierClient(HomeAssistantServiceClient services) : base(services, "humidifier") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantHumidifierAction action, CancellationToken cancellationToken = default)
        => CallAsync(action switch
        {
            HomeAssistantHumidifierAction.TurnOn => "turn_on",
            HomeAssistantHumidifierAction.TurnOff => "turn_off",
            HomeAssistantHumidifierAction.Toggle => "toggle",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported humidifier action.")
        }, target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetHumidityAsync(HomeAssistantTarget target, double humidityPercent, CancellationToken cancellationToken = default)
        => CallAsync("set_humidity", target, call => call.WithData("humidity", ControlValidation.Percent(humidityPercent, nameof(humidityPercent))!.Value), cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetModeAsync(HomeAssistantTarget target, string mode, CancellationToken cancellationToken = default)
        => CallAsync("set_mode", target, call => call.WithData("mode", ControlValidation.RequiredUnchanged(mode, nameof(mode), cancellationToken)), cancellationToken);
}

/// <summary>Controls standard water-heater values without exposing raw payload dictionaries.</summary>
public sealed class HomeAssistantWaterHeaterClient : HomeAssistantControlClientBase
{
    internal HomeAssistantWaterHeaterClient(HomeAssistantServiceClient services) : base(services, "water_heater") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantWaterHeaterAction action, CancellationToken cancellationToken = default)
        => CallAsync(action switch
        {
            HomeAssistantWaterHeaterAction.TurnOn => "turn_on",
            HomeAssistantWaterHeaterAction.TurnOff => "turn_off",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported water heater action.")
        }, target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetTemperatureAsync(HomeAssistantTarget target, double temperature, string? operationMode = null, CancellationToken cancellationToken = default)
        => CallAsync("set_temperature", target, call =>
        {
            call.WithData("temperature", ControlValidation.Finite(temperature, nameof(temperature))!.Value);
            if (operationMode is not null) call.WithData("operation_mode", ControlValidation.RequiredUnchanged(operationMode, nameof(operationMode), cancellationToken));
        }, cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetOperationModeAsync(HomeAssistantTarget target, string operationMode, CancellationToken cancellationToken = default)
        => CallAsync("set_operation_mode", target, call => call.WithData("operation_mode", ControlValidation.RequiredUnchanged(operationMode, nameof(operationMode), cancellationToken)), cancellationToken);

    public Task<HomeAssistantServiceCallResult> SetAwayModeAsync(HomeAssistantTarget target, bool enabled, CancellationToken cancellationToken = default)
        => CallAsync("set_away_mode", target, call => call.WithData("away_mode", enabled), cancellationToken);
}
