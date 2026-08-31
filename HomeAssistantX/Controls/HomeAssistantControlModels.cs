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
    private readonly object _sync = new();
    private double? _brightnessPercent;
    private int? _colorTemperatureKelvin;
    private string? _effect;
    private IReadOnlyList<int>? _rgbColor;
    private TimeSpan? _transition;

    public double? BrightnessPercent
    {
        get { lock (_sync) return _brightnessPercent; }
        set
        {
            var validated = ControlValidation.Percent(value, nameof(BrightnessPercent));
            lock (_sync) _brightnessPercent = validated;
        }
    }

    public int? ColorTemperatureKelvin
    {
        get { lock (_sync) return _colorTemperatureKelvin; }
        set
        {
            var validated = ControlValidation.Positive(value, nameof(ColorTemperatureKelvin));
            lock (_sync)
            {
                if (validated.HasValue && _rgbColor is not null)
                {
                    throw new ArgumentException("ColorTemperatureKelvin and RgbColor cannot be combined.", nameof(ColorTemperatureKelvin));
                }

                _colorTemperatureKelvin = validated;
            }
        }
    }

    public IReadOnlyList<int>? RgbColor
    {
        get
        {
            lock (_sync) return _rgbColor?.ToArray();
        }
        set
        {
            var validated = ControlValidation.Rgb(value, nameof(RgbColor));
            lock (_sync)
            {
                if (validated is not null && _colorTemperatureKelvin.HasValue)
                {
                    throw new ArgumentException("RgbColor and ColorTemperatureKelvin cannot be combined.", nameof(RgbColor));
                }

                _rgbColor = validated;
            }
        }
    }

    public string? Effect
    {
        get { lock (_sync) return _effect; }
        set
        {
            var validated = value is null
                ? null
                : ControlValidation.RequiredUnchanged(value, nameof(Effect));
            lock (_sync) _effect = validated;
        }
    }

    public TimeSpan? Transition
    {
        get { lock (_sync) return _transition; }
        set
        {
            var validated = ControlValidation.Duration(value, nameof(Transition), TimeSpan.FromSeconds(6553));
            lock (_sync) _transition = validated;
        }
    }

    internal void SetValidatedEffect(string? value)
    {
        lock (_sync) _effect = value;
    }

    internal HomeAssistantLightOptions Snapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new HomeAssistantLightOptions
            {
                _brightnessPercent = _brightnessPercent,
                _colorTemperatureKelvin = _colorTemperatureKelvin,
                _effect = _effect,
                _rgbColor = _rgbColor?.ToArray(),
                _transition = _transition
            };
        }
    }

    internal void Apply(HomeAssistantX.Services.HomeAssistantServiceCall call)
    {
        var brightnessPercent = BrightnessPercent;
        var colorTemperatureKelvin = ColorTemperatureKelvin;
        var rgbColor = RgbColor;
        var effect = Effect;
        var transition = Transition;
        if (brightnessPercent.HasValue)
        {
            call.WithData("brightness_pct", brightnessPercent.Value);
        }

        if (colorTemperatureKelvin.HasValue)
        {
            call.WithData("color_temp_kelvin", colorTemperatureKelvin.Value);
        }

        if (rgbColor is not null)
        {
            call.WithData("rgb_color", rgbColor);
        }

        if (effect is not null)
        {
            call.WithData("effect", effect);
        }

        if (transition.HasValue)
        {
            call.WithData("transition", transition.Value.TotalSeconds);
        }
    }
}

/// <summary>Typed climate values that may be applied in one logical operation.</summary>
public sealed class HomeAssistantClimateOptions
{
    private readonly object _sync = new();
    private double? _temperature;
    private double? _targetTemperatureLow;
    private double? _targetTemperatureHigh;
    private string? _hvacMode;
    private string? _fanMode;
    private string? _presetMode;
    private double? _humidity;

    public double? Temperature
    {
        get { lock (_sync) return _temperature; }
        set { lock (_sync) _temperature = value; }
    }

    public double? TargetTemperatureLow
    {
        get { lock (_sync) return _targetTemperatureLow; }
        set { lock (_sync) _targetTemperatureLow = value; }
    }

    public double? TargetTemperatureHigh
    {
        get { lock (_sync) return _targetTemperatureHigh; }
        set { lock (_sync) _targetTemperatureHigh = value; }
    }

    public string? HvacMode
    {
        get { lock (_sync) return _hvacMode; }
        set { lock (_sync) _hvacMode = value; }
    }

    public string? FanMode
    {
        get { lock (_sync) return _fanMode; }
        set { lock (_sync) _fanMode = value; }
    }

    public string? PresetMode
    {
        get { lock (_sync) return _presetMode; }
        set { lock (_sync) _presetMode = value; }
    }

    public double? Humidity
    {
        get { lock (_sync) return _humidity; }
        set
        {
            var validated = ControlValidation.Percent(value, nameof(Humidity));
            lock (_sync) _humidity = validated;
        }
    }

    internal HomeAssistantClimateOptions Snapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new HomeAssistantClimateOptions
            {
                Temperature = _temperature,
                TargetTemperatureLow = _targetTemperatureLow,
                TargetTemperatureHigh = _targetTemperatureHigh,
                HvacMode = _hvacMode,
                FanMode = _fanMode,
                PresetMode = _presetMode,
                Humidity = _humidity
            };
        }
    }

    internal bool HasTemperature => Temperature.HasValue || TargetTemperatureLow.HasValue || TargetTemperatureHigh.HasValue;

    internal void Validate()
        => Validate(default);

    internal void Validate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
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

        HvacMode = NormalizeOptional(HvacMode, nameof(HvacMode), cancellationToken);
        FanMode = PreserveOptional(FanMode, nameof(FanMode), cancellationToken);
        PresetMode = PreserveOptional(PresetMode, nameof(PresetMode), cancellationToken);
    }

    private static string? NormalizeOptional(string? value, string name, CancellationToken cancellationToken)
        => value is null ? null : ControlValidation.Required(value, name, cancellationToken);

    private static string? PreserveOptional(string? value, string name, CancellationToken cancellationToken)
        => value is null ? null : ControlValidation.RequiredUnchanged(value, name, cancellationToken);
}

internal static class ControlValidation
{
    public static string Required(string? value, string name)
        => Required(value, name, default);

    public static string Required(
        string? value,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null)
        {
            throw new ArgumentException("A non-empty value is required.", name);
        }

        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            if ((start & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            start++;
        }
        if (start == value.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ArgumentException("A non-empty value is required.", name);
        }

        var end = value.Length - 1;
        while (end > start && char.IsWhiteSpace(value[end]))
        {
            if (((value.Length - end) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            end--;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return HomeAssistantX.Protocol.CancellationAwareString.Slice(
            value,
            start,
            end - start + 1,
            cancellationToken);
    }

    public static string RequiredUnchanged(
        string? value,
        string name,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null)
        {
            throw new ArgumentException("A non-empty value is required.", name);
        }

        var hasContent = false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!char.IsWhiteSpace(value[index])) hasContent = true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!hasContent)
        {
            throw new ArgumentException("A non-empty value is required.", name);
        }

        return value;
    }

    public static IReadOnlyList<string> RequiredValues(
        IReadOnlyList<string>? values,
        string name,
        CancellationToken cancellationToken = default)
        => RequiredValuesCore(values, name, preserveWhitespace: false, cancellationToken);

    public static IReadOnlyList<string> RequiredValuesUnchanged(
        IReadOnlyList<string>? values,
        string name,
        CancellationToken cancellationToken = default)
        => RequiredValuesCore(values, name, preserveWhitespace: true, cancellationToken);

    private static IReadOnlyList<string> RequiredValuesCore(
        IReadOnlyList<string>? values,
        string name,
        bool preserveWhitespace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (values is null)
        {
            throw new ArgumentException("At least one non-empty value is required.", name);
        }

        var count = values.Count;
        if (count == 0)
        {
            throw new ArgumentException("At least one non-empty value is required.", name);
        }

        var normalized = new string[count];
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            normalized[index] = preserveWhitespace
                ? RequiredUnchanged(values[index], name, cancellationToken)
                : Required(values[index], name, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return normalized;
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

    public static int PercentInt(int value, string name)
    {
        if (value < 0 || value > 100) throw new ArgumentOutOfRangeException(name, "The value must be between 0 and 100.");
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
