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
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        cancellationToken.ThrowIfCancellationRequested();
        options.Validate();
        var temperature = options.Temperature;
        var targetTemperatureLow = options.TargetTemperatureLow;
        var targetTemperatureHigh = options.TargetTemperatureHigh;
        var hvacMode = options.HvacMode;
        var fanMode = options.FanMode;
        var presetMode = options.PresetMode;
        var humidity = options.Humidity;
        var hasTemperature = temperature.HasValue || targetTemperatureLow.HasValue || targetTemperatureHigh.HasValue;
        if (!hasTemperature
            && string.IsNullOrWhiteSpace(hvacMode)
            && string.IsNullOrWhiteSpace(fanMode)
            && string.IsNullOrWhiteSpace(presetMode)
            && !humidity.HasValue)
        {
            throw new ArgumentException("At least one climate value is required.", nameof(options));
        }

        var context = CaptureContext(cancellationToken);
        var frozenTarget = (target ?? throw new ArgumentNullException(nameof(target)))
            .NormalizeForDomain(Domain, cancellationToken);
        var results = new List<HomeAssistantServiceCallResult>();
        if (hasTemperature)
        {
            results.Add(await CallAsync("set_temperature", frozenTarget, call =>
            {
                if (temperature.HasValue)
                {
                    call.WithData("temperature", temperature.Value);
                }

                if (targetTemperatureLow.HasValue)
                {
                    call.WithData("target_temp_low", targetTemperatureLow.Value);
                }

                if (targetTemperatureHigh.HasValue)
                {
                    call.WithData("target_temp_high", targetTemperatureHigh.Value);
                }

                if (!string.IsNullOrWhiteSpace(hvacMode))
                {
                    call.WithData("hvac_mode", hvacMode);
                }
            }, context, cancellationToken).ConfigureAwait(false));
        }
        else if (!string.IsNullOrWhiteSpace(hvacMode))
        {
            results.Add(await CallAsync("set_hvac_mode", frozenTarget, call => call.WithData("hvac_mode", hvacMode), context, cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(fanMode))
        {
            results.Add(await CallAsync("set_fan_mode", frozenTarget, call => call.WithData("fan_mode", fanMode), context, cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(presetMode))
        {
            results.Add(await CallAsync("set_preset_mode", frozenTarget, call => call.WithData("preset_mode", presetMode), context, cancellationToken).ConfigureAwait(false));
        }

        if (humidity.HasValue)
        {
            results.Add(await CallAsync("set_humidity", frozenTarget, call => call.WithData("humidity", humidity.Value), context, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }
}
