using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

/// <summary>Invokes standard Home Assistant climate actions with typed values.</summary>
public sealed class HomeAssistantClimateClient : HomeAssistantControlClientBase
{
    internal HomeAssistantClimateClient(HomeAssistantServiceClient services) : base(services, "climate") { }

    public async Task<IReadOnlyList<HomeAssistantServiceCallResult>> SetAsync(
        HomeAssistantTarget target,
        HomeAssistantClimateOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        var results = new List<HomeAssistantServiceCallResult>();
        if (options.HasTemperature)
        {
            results.Add(await CallAsync("set_temperature", target, call =>
            {
                if (options.Temperature.HasValue) call.WithData("temperature", options.Temperature.Value);
                if (options.TargetTemperatureLow.HasValue) call.WithData("target_temp_low", options.TargetTemperatureLow.Value);
                if (options.TargetTemperatureHigh.HasValue) call.WithData("target_temp_high", options.TargetTemperatureHigh.Value);
                if (!string.IsNullOrWhiteSpace(options.HvacMode)) call.WithData("hvac_mode", options.HvacMode);
            }, cancellationToken).ConfigureAwait(false));
        }
        else if (!string.IsNullOrWhiteSpace(options.HvacMode))
        {
            results.Add(await CallAsync("set_hvac_mode", target, call => call.WithData("hvac_mode", options.HvacMode), cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(options.FanMode)) results.Add(await CallAsync("set_fan_mode", target, call => call.WithData("fan_mode", options.FanMode), cancellationToken).ConfigureAwait(false));
        if (!string.IsNullOrWhiteSpace(options.PresetMode)) results.Add(await CallAsync("set_preset_mode", target, call => call.WithData("preset_mode", options.PresetMode), cancellationToken).ConfigureAwait(false));
        if (options.Humidity.HasValue) results.Add(await CallAsync("set_humidity", target, call => call.WithData("humidity", options.Humidity.Value), cancellationToken).ConfigureAwait(false));
        if (results.Count == 0) throw new ArgumentException("At least one climate value is required.", nameof(options));
        return results;
    }
}
