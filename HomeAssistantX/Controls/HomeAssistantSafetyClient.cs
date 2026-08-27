using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

public enum HomeAssistantAlarmAction
{
    Disarm,
    ArmHome,
    ArmAway,
    ArmNight,
    ArmVacation,
    ArmCustomBypass,
    Trigger
}

public enum HomeAssistantSirenAction
{
    TurnOn,
    TurnOff,
    Toggle
}

public sealed class HomeAssistantSirenOptions
{
    private double? _volumePercent;
    private TimeSpan? _duration;
    private string? _tone;
    private int? _toneId;

    public string? Tone
    {
        get => _tone;
        set
        {
            if (value is null)
            {
                _tone = null;
                return;
            }

            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Tone cannot be blank.", nameof(Tone));
            if (_toneId.HasValue) throw new ArgumentException("Tone and ToneId cannot be combined.", nameof(Tone));
            _tone = value.Trim();
        }
    }

    public int? ToneId
    {
        get => _toneId;
        set
        {
            if (value.HasValue && _tone is not null) throw new ArgumentException("ToneId and Tone cannot be combined.", nameof(ToneId));
            _toneId = value;
        }
    }

    public double? VolumePercent
    {
        get => _volumePercent;
        set => _volumePercent = ControlValidation.Percent(value, nameof(VolumePercent));
    }

    public TimeSpan? Duration
    {
        get => _duration;
        set
        {
            if (value.HasValue && (value.Value <= TimeSpan.Zero || value.Value.TotalSeconds > int.MaxValue || value.Value.TotalSeconds != Math.Truncate(value.Value.TotalSeconds)))
                throw new ArgumentOutOfRangeException(nameof(Duration), "Siren duration must be a positive whole number of seconds.");
            _duration = value;
        }
    }

    internal void Apply(HomeAssistantServiceCall call)
    {
        if (Tone is not null) call.WithData("tone", Tone);
        if (ToneId.HasValue) call.WithData("tone", ToneId.Value);
        if (VolumePercent.HasValue) call.WithData("volume_level", VolumePercent.Value / 100d);
        if (Duration.HasValue) call.WithData("duration", (int)Duration.Value.TotalSeconds);
    }
}

/// <summary>Controls alarm panels; callers remain responsible for product authorization and confirmation policy.</summary>
public sealed class HomeAssistantAlarmClient : HomeAssistantControlClientBase
{
    internal HomeAssistantAlarmClient(HomeAssistantServiceClient services) : base(services, "alarm_control_panel") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantAlarmAction action, string? code = null, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code is null ? null : ControlValidation.RequiredUnchanged(code, nameof(code));
        return CallAsync(action switch
        {
            HomeAssistantAlarmAction.Disarm => "alarm_disarm",
            HomeAssistantAlarmAction.ArmHome => "alarm_arm_home",
            HomeAssistantAlarmAction.ArmAway => "alarm_arm_away",
            HomeAssistantAlarmAction.ArmNight => "alarm_arm_night",
            HomeAssistantAlarmAction.ArmVacation => "alarm_arm_vacation",
            HomeAssistantAlarmAction.ArmCustomBypass => "alarm_arm_custom_bypass",
            HomeAssistantAlarmAction.Trigger => "alarm_trigger",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported alarm action.")
        }, target, call =>
        {
            if (normalizedCode is not null) call.WithData("code", normalizedCode);
        }, cancellationToken);
    }
}

/// <summary>Controls standard siren actions and their portable optional fields.</summary>
public sealed class HomeAssistantSirenClient : HomeAssistantControlClientBase
{
    internal HomeAssistantSirenClient(HomeAssistantServiceClient services) : base(services, "siren") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantSirenAction action, HomeAssistantSirenOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (options is not null && action != HomeAssistantSirenAction.TurnOn)
        {
            throw new ArgumentException("Siren options are valid only when turning a siren on.", nameof(options));
        }

        return CallAsync(action switch
        {
            HomeAssistantSirenAction.TurnOn => "turn_on",
            HomeAssistantSirenAction.TurnOff => "turn_off",
            HomeAssistantSirenAction.Toggle => "toggle",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported siren action.")
        }, target, options is null ? null : options.Apply, cancellationToken);
    }
}
