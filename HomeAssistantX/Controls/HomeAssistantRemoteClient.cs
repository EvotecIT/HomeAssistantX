using HomeAssistantX.Configuration;
using HomeAssistantX.Models;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.Controls;

/// <summary>Reads and controls standard Home Assistant <c>remote</c> entities.</summary>
public sealed class HomeAssistantRemoteClient : HomeAssistantControlClientBase
{
    private const int DefaultLearningTimeoutSeconds = 30;
    private static readonly TimeSpan LearningResponseMargin = TimeSpan.FromSeconds(1);
    private readonly HomeAssistantStateClient _states;
    private readonly HomeAssistantClientOptions _options;

    internal HomeAssistantRemoteClient(
        HomeAssistantServiceClient services,
        HomeAssistantStateClient states,
        HomeAssistantClientOptions options)
        : base(services, "remote")
    {
        _states = states;
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<HomeAssistantRemoteStatus> GetAsync(
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, Domain, out var normalizedEntityId))
            throw new ArgumentException("A remote entity identifier is required.", nameof(entityId));
        var state = await _states.GetAsync(normalizedEntityId, cancellationToken).ConfigureAwait(false);
        return HomeAssistantRemoteStatus.FromState(HomeAssistantEntityId.RequireResponseDomain(state, Domain));
    }

    public async Task<IReadOnlyList<HomeAssistantRemoteStatus>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var states = await _states.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return states
            .Where(state => string.Equals(state.Domain, Domain, StringComparison.OrdinalIgnoreCase))
            .Select(state => HomeAssistantRemoteStatus.FromState(
                HomeAssistantEntityId.RequireResponseDomain(state, Domain)))
            .ToArray();
    }

    public Task<IHomeAssistantSubscription> SubscribeAsync(
        Func<HomeAssistantRemoteStateChange, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return _states.SubscribeAsync(
            HomeAssistantStateFilter.ForDomains(Domain),
            (change, token) => handler(
                new HomeAssistantRemoteStateChange(
                    change.EntityId,
                    ToStatus(change.PreviousState),
                    ToStatus(change.CurrentState)),
                token),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetPowerAsync(
        HomeAssistantTarget target,
        HomeAssistantPowerAction action,
        string? activity = null,
        CancellationToken cancellationToken = default)
    {
        var service = action switch
        {
            HomeAssistantPowerAction.On => "turn_on",
            HomeAssistantPowerAction.Off => "turn_off",
            HomeAssistantPowerAction.Toggle => "toggle",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported remote power action.")
        };
        return CallAsync(
            service,
            target,
            string.IsNullOrWhiteSpace(activity)
                ? null
                : call => call.WithData("activity", activity),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SendCommandsAsync(
        HomeAssistantTarget target,
        IEnumerable<string> commands,
        HomeAssistantRemoteSendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var values = ValidateCommands(commands, nameof(commands));
        return CallAsync(
            "send_command",
            target,
            call =>
            {
                call.WithData("command", values);
                if (!string.IsNullOrWhiteSpace(options?.Device))
                {
                    call.WithData("device", options!.Device);
                }

                if (options?.RepeatCount.HasValue == true)
                {
                    call.WithData("num_repeats", options.RepeatCount.Value);
                }

                if (options?.Delay.HasValue == true)
                {
                    call.WithData("delay_secs", options.Delay.Value.TotalSeconds);
                }

                if (options?.Hold.HasValue == true)
                {
                    call.WithData("hold_secs", options.Hold.Value.TotalSeconds);
                }
            },
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> LearnCommandsAsync(
        HomeAssistantTarget target,
        HomeAssistantRemoteLearnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var learningTimeoutSeconds = ResolveLearningTimeoutSeconds(
            options?.Timeout,
            _options.RequestTimeout,
            nameof(options));

        string? commandType = null;
        if (options?.CommandType.HasValue == true)
        {
            commandType = options.CommandType.Value switch
            {
                HomeAssistantRemoteCommandType.Ir => "ir",
                HomeAssistantRemoteCommandType.Rf => "rf",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.CommandType),
                    options.CommandType.Value,
                    "Unsupported remote command type.")
            };
        }

        IReadOnlyList<string>? commands = null;
        if (options?.Commands is not null)
        {
            commands = ValidateCommands(options.Commands, nameof(options.Commands));
        }

        return CallAsync(
            "learn_command",
            target,
            call =>
            {
                if (!string.IsNullOrWhiteSpace(options?.Device))
                {
                    call.WithData("device", options!.Device);
                }

                if (commands is not null)
                {
                    call.WithData("command", commands);
                }

                if (commandType is not null)
                {
                    call.WithData("command_type", commandType);
                }

                if (options?.Alternative.HasValue == true)
                {
                    call.WithData("alternative", options.Alternative.Value);
                }

                call.WithData("timeout", learningTimeoutSeconds);
            },
            cancellationToken);
    }

    internal static int ResolveLearningTimeoutSeconds(
        TimeSpan? learningTimeout,
        TimeSpan requestTimeout,
        string parameterName)
    {
        var available = requestTimeout > LearningResponseMargin
            ? requestTimeout - LearningResponseMargin
            : TimeSpan.Zero;
        var effectiveSeconds = learningTimeout.HasValue
            ? Math.Ceiling(learningTimeout.Value.TotalSeconds)
            : Math.Min(DefaultLearningTimeoutSeconds, Math.Floor(available.TotalSeconds));
        if (effectiveSeconds < 1d || TimeSpan.FromSeconds(effectiveSeconds) > available)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The remote learning timeout must leave at least one second inside the configured request timeout of {requestTimeout.TotalSeconds:g} seconds for dispatch and response handling.");
        }

        return checked((int)effectiveSeconds);
    }

    public Task<HomeAssistantServiceCallResult> DeleteCommandsAsync(
        HomeAssistantTarget target,
        IEnumerable<string> commands,
        string? device = null,
        CancellationToken cancellationToken = default)
    {
        var values = ValidateCommands(commands, nameof(commands));
        return CallAsync(
            "delete_command",
            target,
            call =>
            {
                call.WithData("command", values);
                if (!string.IsNullOrWhiteSpace(device))
                {
                    call.WithData("device", device);
                }
            },
            cancellationToken);
    }

    private static IReadOnlyList<string> ValidateCommands(
        IEnumerable<string> commands,
        string parameterName)
    {
        if (commands is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var values = commands.ToArray();
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException(
                "At least one non-empty remote command is required.",
                parameterName);
        }

        return values;
    }

    private static HomeAssistantRemoteStatus? ToStatus(HomeAssistantState? state)
    {
        return state is null
            ? null
            : HomeAssistantRemoteStatus.FromState(HomeAssistantEntityId.RequireResponseDomain(state, "remote"));
    }
}
