using HomeAssistantX.Models;
using HomeAssistantX.Exceptions;

namespace HomeAssistantX.Controls;

/// <summary>Optional capabilities exposed through a Home Assistant <c>remote</c> entity.</summary>
[Flags]
public enum HomeAssistantRemoteFeature
{
    None = 0,
    LearnCommand = 1,
    DeleteCommand = 2,
    Activity = 4
}

public enum HomeAssistantRemoteCommandType
{
    Ir,
    Rf
}

/// <summary>Options accepted by Home Assistant's <c>remote.send_command</c> action.</summary>
public sealed class HomeAssistantRemoteSendOptions
{
    private int? _repeatCount;
    private TimeSpan? _delay;
    private TimeSpan? _hold;

    public string? Device { get; set; }

    public int? RepeatCount
    {
        get => _repeatCount;
        set
        {
            if (value.HasValue && value.Value < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(RepeatCount), "RepeatCount must be at least one.");
            }

            _repeatCount = value;
        }
    }

    public TimeSpan? Delay
    {
        get => _delay;
        set => _delay = ControlValidation.Duration(value, nameof(Delay));
    }

    public TimeSpan? Hold
    {
        get => _hold;
        set => _hold = ControlValidation.Duration(value, nameof(Hold));
    }
}

/// <summary>Options accepted when a Home Assistant remote learns commands.</summary>
public sealed class HomeAssistantRemoteLearnOptions
{
    private TimeSpan? _timeout;

    public string? Device { get; set; }

    public IReadOnlyList<string>? Commands { get; set; }

    public HomeAssistantRemoteCommandType? CommandType { get; set; }

    public bool? Alternative { get; set; }

    public TimeSpan? Timeout
    {
        get => _timeout;
        set
        {
            var validated = ControlValidation.Duration(value, nameof(Timeout));
            if (validated.HasValue
                && (validated.Value <= TimeSpan.Zero || validated.Value.TotalSeconds > int.MaxValue))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Timeout),
                    $"Timeout must be greater than zero and no longer than {int.MaxValue} seconds.");
            }

            _timeout = validated;
        }
    }
}

/// <summary>A typed view of one raw Home Assistant remote state.</summary>
public sealed class HomeAssistantRemoteStatus
{
    private HomeAssistantRemoteStatus(HomeAssistantState rawState)
    {
        RawState = rawState;
    }

    public HomeAssistantState RawState { get; }

    public string EntityId => RawState.EntityId;

    public string RawStateValue => RawState.State;

    public bool? IsOn { get; private set; }

    public bool IsAvailable => !string.Equals(RawState.State, "unavailable", StringComparison.OrdinalIgnoreCase);

    public string? FriendlyName { get; private set; }

    public HomeAssistantRemoteFeature SupportedFeatures { get; private set; }

    public string? CurrentActivity { get; private set; }

    public IReadOnlyList<string> Activities { get; private set; } = Array.Empty<string>();

    public bool Supports(HomeAssistantRemoteFeature feature)
    {
        return feature != HomeAssistantRemoteFeature.None
            && (SupportedFeatures & feature) == feature;
    }

    public static HomeAssistantRemoteStatus FromState(HomeAssistantState state)
        => FromState(state, default);

    internal static HomeAssistantRemoteStatus FromState(
        HomeAssistantState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (!HomeAssistantEntityId.TryNormalizeForDomain(state.EntityId, "remote", out var normalizedEntityId)
            || !string.Equals(state.EntityId, normalizedEntityId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A canonical remote entity state is required.", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(state.State))
        {
            throw new HomeAssistantProtocolException("The Home Assistant remote state omitted its required state value.");
        }

        var attributes = state.Attributes;
        return new HomeAssistantRemoteStatus(state)
        {
            IsOn = string.Equals(state.State, "on", StringComparison.OrdinalIgnoreCase)
                ? true
                : string.Equals(state.State, "off", StringComparison.OrdinalIgnoreCase)
                    ? false
                    : null,
            FriendlyName = HomeAssistantAttributeReader.GetString(attributes, "friendly_name"),
            SupportedFeatures = (HomeAssistantRemoteFeature)(HomeAssistantAttributeReader.GetNonNegativeInt32(attributes, "supported_features") ?? 0),
            CurrentActivity = HomeAssistantAttributeReader.GetString(attributes, "current_activity"),
            Activities = HomeAssistantAttributeReader.GetStringList(attributes, "activity_list", cancellationToken)
        };
    }
}

public sealed class HomeAssistantRemoteStateChange
{
    internal HomeAssistantRemoteStateChange(
        string entityId,
        HomeAssistantRemoteStatus? previous,
        HomeAssistantRemoteStatus? current)
    {
        EntityId = entityId;
        Previous = previous;
        Current = current;
    }

    public string EntityId { get; }

    public HomeAssistantRemoteStatus? Previous { get; }

    public HomeAssistantRemoteStatus? Current { get; }

    public bool IsRemoval => Current is null;
}
