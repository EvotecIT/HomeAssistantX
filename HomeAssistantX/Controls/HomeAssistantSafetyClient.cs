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
    private readonly object _sync = new();
    private double? _volumePercent;
    private TimeSpan? _duration;
    private string? _tone;
    private int? _toneId;

    public string? Tone
    {
        get { lock (_sync) return _tone; }
        set
        {
            lock (_sync)
            {
                if (value is null)
                {
                    _tone = null;
                    return;
                }

                if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Tone cannot be blank.", nameof(Tone));
                if (_toneId.HasValue) throw new ArgumentException("Tone and ToneId cannot be combined.", nameof(Tone));
                _tone = value;
            }
        }
    }

    public int? ToneId
    {
        get { lock (_sync) return _toneId; }
        set
        {
            lock (_sync)
            {
                if (value.HasValue && _tone is not null) throw new ArgumentException("ToneId and Tone cannot be combined.", nameof(ToneId));
                _toneId = value;
            }
        }
    }

    public double? VolumePercent
    {
        get { lock (_sync) return _volumePercent; }
        set
        {
            var validated = ControlValidation.Percent(value, nameof(VolumePercent));
            lock (_sync) _volumePercent = validated;
        }
    }

    public TimeSpan? Duration
    {
        get { lock (_sync) return _duration; }
        set
        {
            if (value.HasValue && (value.Value <= TimeSpan.Zero || value.Value.TotalSeconds > int.MaxValue || value.Value.TotalSeconds != Math.Truncate(value.Value.TotalSeconds)))
                throw new ArgumentOutOfRangeException(nameof(Duration), "Siren duration must be a positive whole number of seconds.");
            lock (_sync) _duration = value;
        }
    }

    internal void SetValidatedTone(string? value)
    {
        lock (_sync)
        {
            if (value is not null && _toneId.HasValue)
                throw new ArgumentException("Tone and ToneId cannot be combined.", nameof(Tone));
            _tone = value;
        }
    }

    internal HomeAssistantSirenOptions Snapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new HomeAssistantSirenOptions
            {
                _tone = _tone,
                _toneId = _toneId,
                _volumePercent = _volumePercent,
                _duration = _duration
            };
        }
    }
}

/// <summary>Controls alarm panels; callers remain responsible for product authorization and confirmation policy.</summary>
public sealed class HomeAssistantAlarmClient : HomeAssistantControlClientBase
{
    internal HomeAssistantAlarmClient(HomeAssistantServiceClient services) : base(services, "alarm_control_panel") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantAlarmAction action, string? code = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedCode = code is null ? null : ControlValidation.RequiredUnchanged(code, nameof(code), cancellationToken);
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
        cancellationToken.ThrowIfCancellationRequested();
        if (options is not null && action != HomeAssistantSirenAction.TurnOn)
        {
            throw new ArgumentException("Siren options are valid only when turning a siren on.", nameof(options));
        }

        var frozenOptions = options?.Snapshot(cancellationToken);
        var tone = frozenOptions?.Tone;
        var toneId = frozenOptions?.ToneId;
        var volumePercent = frozenOptions?.VolumePercent;
        var duration = frozenOptions?.Duration;
        if (tone is not null)
        {
            tone = ControlValidation.RequiredUnchanged(tone, nameof(options), cancellationToken);
        }
        if (tone is not null && toneId.HasValue)
        {
            throw new ArgumentException("Tone and ToneId cannot be combined.", nameof(options));
        }
        cancellationToken.ThrowIfCancellationRequested();

        return CallAsync(action switch
        {
            HomeAssistantSirenAction.TurnOn => "turn_on",
            HomeAssistantSirenAction.TurnOff => "turn_off",
            HomeAssistantSirenAction.Toggle => "toggle",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported siren action.")
        }, target, options is null ? null : call =>
        {
            if (tone is not null) call.WithData("tone", tone);
            if (toneId.HasValue) call.WithData("tone", toneId.Value);
            if (volumePercent.HasValue) call.WithData("volume_level", volumePercent.Value / 100d);
            if (duration.HasValue) call.WithData("duration", (int)duration.Value.TotalSeconds);
        }, cancellationToken);
    }
}
