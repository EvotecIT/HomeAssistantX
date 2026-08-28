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
        return HomeAssistantRemoteStatus.FromState(
            HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantRemoteStatus>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var states = await _states.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<HomeAssistantRemoteStatus>();
        foreach (var state in HomeAssistantEntityId.RequireResponseDomainStates(states, Domain, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(HomeAssistantRemoteStatus.FromState(state, cancellationToken));
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result;
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
                    ToStatus(change.PreviousState, token),
                    ToStatus(change.CurrentState, token)),
                token),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetPowerAsync(
        HomeAssistantTarget target,
        HomeAssistantPowerAction action,
        string? activity = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedActivity = activity is null ? null : RequiredSelector(activity, nameof(activity));
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
            normalizedActivity is null
                ? null
                : call => call.WithData("activity", normalizedActivity),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SendCommandsAsync(
        HomeAssistantTarget target,
        IEnumerable<string> commands,
        HomeAssistantRemoteSendOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var values = ValidateCommands(commands, nameof(commands), cancellationToken);
        var device = options?.Device is null ? null : RequiredSelector(options.Device, nameof(options.Device));
        var repeatCount = options?.RepeatCount;
        var delay = options?.Delay;
        var hold = options?.Hold;
        return CallAsync(
            "send_command",
            target,
            call =>
            {
                call.WithData("command", values);
                if (device is not null)
                {
                    call.WithData("device", device);
                }

                if (repeatCount.HasValue)
                {
                    call.WithData("num_repeats", repeatCount.Value);
                }

                if (delay.HasValue)
                {
                    call.WithData("delay_secs", delay.Value.TotalSeconds);
                }

                if (hold.HasValue)
                {
                    call.WithData("hold_secs", hold.Value.TotalSeconds);
                }
            },
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> LearnCommandsAsync(
        HomeAssistantTarget target,
        HomeAssistantRemoteLearnOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var device = options?.Device is null ? null : RequiredSelector(options.Device, nameof(options.Device));
        var timeout = options?.Timeout;
        var commandTypeOption = options?.CommandType;
        var alternative = options?.Alternative;
        var optionCommands = options?.Commands;
        var transport = CaptureTransport(cancellationToken);
        var requestTimeout = _options.RequestTimeout;
        var learningTimeoutSeconds = ResolveLearningTimeoutSeconds(
            timeout,
            requestTimeout,
            nameof(options));

        string? commandType = null;
        if (commandTypeOption.HasValue)
        {
            commandType = commandTypeOption.Value switch
            {
                HomeAssistantRemoteCommandType.Ir => "ir",
                HomeAssistantRemoteCommandType.Rf => "rf",
                _ => throw new ArgumentOutOfRangeException(
                    nameof(options.CommandType),
                    commandTypeOption.Value,
                    "Unsupported remote command type.")
            };
        }

        IReadOnlyList<string>? commands = null;
        if (optionCommands is not null)
        {
            commands = ValidateCommands(optionCommands, nameof(options.Commands), cancellationToken);
        }

        return CallAsync(
            "learn_command",
            target,
            call =>
            {
                if (device is not null)
                {
                    call.WithData("device", device);
                }

                if (commands is not null)
                {
                    call.WithData("command", commands);
                }

                if (commandType is not null)
                {
                    call.WithData("command_type", commandType);
                }

                if (alternative.HasValue)
                {
                    call.WithData("alternative", alternative.Value);
                }

                call.WithData("timeout", learningTimeoutSeconds);
            },
            transport,
            requestTimeout,
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
        cancellationToken.ThrowIfCancellationRequested();
        var values = ValidateCommands(commands, nameof(commands), cancellationToken);
        var normalizedDevice = device is null ? null : RequiredSelector(device, nameof(device));
        return CallAsync(
            "delete_command",
            target,
            call =>
            {
                call.WithData("command", values);
                if (normalizedDevice is not null)
                {
                    call.WithData("device", normalizedDevice);
                }
            },
            cancellationToken);
    }

    private static IReadOnlyList<string> ValidateCommands(
        IEnumerable<string> commands,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (commands is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var values = new List<string>();
        foreach (var command in commands)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException(
                    "At least one non-empty remote command is required.",
                    parameterName);
            }

            values.Add(command);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (values.Count == 0)
        {
            throw new ArgumentException(
                "At least one non-empty remote command is required.",
                parameterName);
        }

        return values;
    }

    private static string RequiredSelector(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-empty selector is required.", parameterName);
        return value;
    }

    private static HomeAssistantRemoteStatus? ToStatus(
        HomeAssistantState? state,
        CancellationToken cancellationToken)
    {
        return state is null
            ? null
            : HomeAssistantRemoteStatus.FromState(
                HomeAssistantEntityId.RequireResponseDomain(state, "remote"),
                cancellationToken);
    }
}
