using HomeAssistantX.Protocol;

namespace HomeAssistantX.Controls;

public enum HomeAssistantPowerAction
{
    On,
    Off,
    Toggle
}

public enum HomeAssistantCoverAction
{
    Open,
    Close,
    Stop,
    Toggle
}

public enum HomeAssistantLockAction
{
    Lock,
    Unlock,
    Open
}

/// <summary>Typed inputs accepted when a light is turned on or toggled.</summary>
public sealed class HomeAssistantLightOptions
{
    private double? _brightnessPercent;
    private int? _colorTemperatureKelvin;
    private IReadOnlyList<int>? _rgbColor;
    private TimeSpan? _transition;

    public double? BrightnessPercent
    {
        get => _brightnessPercent;
        set => _brightnessPercent = ControlValidation.Percent(value, nameof(BrightnessPercent));
    }

    public int? ColorTemperatureKelvin
    {
        get => _colorTemperatureKelvin;
        set
        {
            var validated = ControlValidation.Positive(value, nameof(ColorTemperatureKelvin));
            if (validated.HasValue && _rgbColor is not null)
            {
                throw new ArgumentException("ColorTemperatureKelvin and RgbColor cannot be combined.", nameof(ColorTemperatureKelvin));
            }

            _colorTemperatureKelvin = validated;
        }
    }

    public IReadOnlyList<int>? RgbColor
    {
        get => _rgbColor;
        set
        {
            var validated = ControlValidation.Rgb(value, nameof(RgbColor));
            if (validated is not null && _colorTemperatureKelvin.HasValue)
            {
                throw new ArgumentException("RgbColor and ColorTemperatureKelvin cannot be combined.", nameof(RgbColor));
            }

            _rgbColor = validated;
        }
    }

    public string? Effect { get; set; }

    public TimeSpan? Transition
    {
        get => _transition;
        set => _transition = ControlValidation.Duration(value, nameof(Transition), TimeSpan.FromSeconds(6553));
    }

    internal void Apply(HomeAssistantX.Services.HomeAssistantServiceCall call)
    {
        if (BrightnessPercent.HasValue)
        {
            call.WithData("brightness_pct", BrightnessPercent.Value);
        }

        if (ColorTemperatureKelvin.HasValue)
        {
            call.WithData("color_temp_kelvin", ColorTemperatureKelvin.Value);
        }

        if (RgbColor is not null)
        {
            call.WithData("rgb_color", RgbColor);
        }

        if (!string.IsNullOrWhiteSpace(Effect))
        {
            call.WithData("effect", Effect);
        }

        if (Transition.HasValue)
        {
            call.WithData("transition", Transition.Value.TotalSeconds);
        }
    }
}

/// <summary>Typed climate values that may be applied in one logical operation.</summary>
public sealed class HomeAssistantClimateOptions
{
    private double? _humidity;

    public double? Temperature { get; set; }

    public double? TargetTemperatureLow { get; set; }

    public double? TargetTemperatureHigh { get; set; }

    public string? HvacMode { get; set; }

    public string? FanMode { get; set; }

    public string? PresetMode { get; set; }

    public double? Humidity
    {
        get => _humidity;
        set => _humidity = ControlValidation.Percent(value, nameof(Humidity));
    }

    internal bool HasTemperature => Temperature.HasValue || TargetTemperatureLow.HasValue || TargetTemperatureHigh.HasValue;

    internal void Validate()
    {
        ControlValidation.Finite(Temperature, nameof(Temperature));
        ControlValidation.Finite(TargetTemperatureLow, nameof(TargetTemperatureLow));
        ControlValidation.Finite(TargetTemperatureHigh, nameof(TargetTemperatureHigh));

        var hasLow = TargetTemperatureLow.HasValue;
        var hasHigh = TargetTemperatureHigh.HasValue;
        if (hasLow != hasHigh)
        {
            throw new ArgumentException("TargetTemperatureLow and TargetTemperatureHigh must be supplied together.");
        }

        if (Temperature.HasValue && hasLow)
        {
            throw new ArgumentException("Temperature cannot be combined with a target temperature range.");
        }

        if (hasLow && TargetTemperatureLow!.Value > TargetTemperatureHigh!.Value)
        {
            throw new ArgumentException("TargetTemperatureLow cannot be greater than TargetTemperatureHigh.");
        }
    }
}

internal static class ControlValidation
{
    public static string Required(
        string? value,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken))
        {
            throw new ArgumentException("A non-empty value is required.", name);
        }

        return CancellationAwareString.Trim(value!, cancellationToken);
    }

    public static string RequiredUnchanged(
        string? value,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken))
        {
            throw new ArgumentException("A non-empty value is required.", name);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return value!;
    }

    public static double? Percent(double? value, string name)
    {
        if (value.HasValue && (!IsFinite(value.Value) || value.Value < 0 || value.Value > 100))
        {
            throw new ArgumentOutOfRangeException(name, "The value must be between 0 and 100.");
        }

        return value;
    }

    public static double? Finite(double? value, string name)
    {
        if (value.HasValue && !IsFinite(value.Value))
        {
            throw new ArgumentOutOfRangeException(name, "The value must be a finite number.");
        }

        return value;
    }

    public static int? Positive(int? value, string name)
    {
        if (value.HasValue && value.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, "The value must be greater than zero.");
        }

        return value;
    }

    public static TimeSpan? Duration(TimeSpan? value, string name, TimeSpan? maximum = null)
    {
        if (value.HasValue && (value.Value < TimeSpan.Zero || (maximum.HasValue && value.Value > maximum.Value)))
        {
            throw new ArgumentOutOfRangeException(name, "The duration is outside the supported range.");
        }

        return value;
    }

    public static IReadOnlyList<int>? Rgb(IReadOnlyList<int>? value, string name)
    {
        if (value is not null && (value.Count != 3 || value.Any(component => component < 0 || component > 255)))
        {
            throw new ArgumentException("RGB color must contain exactly three values between 0 and 255.", name);
        }

        return value?.ToArray();
    }

    private static bool IsFinite(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
