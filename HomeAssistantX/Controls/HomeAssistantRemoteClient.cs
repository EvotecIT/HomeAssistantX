using HomeAssistantX.Models;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.Controls;

/// <summary>Reads and controls standard Home Assistant <c>remote</c> entities.</summary>
public sealed class HomeAssistantRemoteClient : HomeAssistantControlClientBase
{
    private readonly HomeAssistantStateClient _states;
    private readonly TimeSpan _requestTimeout;

    internal HomeAssistantRemoteClient(
        HomeAssistantServiceClient services,
        HomeAssistantStateClient states,
        TimeSpan requestTimeout)
        : base(services, "remote")
    {
        _states = states;
        _requestTimeout = requestTimeout;
    }

    public async Task<HomeAssistantRemoteStatus> GetAsync(
        string entityId,
        CancellationToken cancellationToken = default)
    {
        return HomeAssistantRemoteStatus.FromState(
            await _states.GetAsync(entityId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<HomeAssistantRemoteStatus>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var states = await _states.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return states
            .Where(state => string.Equals(state.Domain, Domain, StringComparison.OrdinalIgnoreCase))
            .Select(HomeAssistantRemoteStatus.FromState)
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
        ValidateLearningTimeout(options?.Timeout, _requestTimeout, nameof(options));

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

                if (options?.Timeout.HasValue == true)
                {
                    call.WithData("timeout", checked((int)Math.Ceiling(options.Timeout.Value.TotalSeconds)));
                }
            },
            cancellationToken);
    }

    internal static void ValidateLearningTimeout(
        TimeSpan? learningTimeout,
        TimeSpan requestTimeout,
        string parameterName)
    {
        if (learningTimeout.HasValue
            && TimeSpan.FromSeconds(Math.Ceiling(learningTimeout.Value.TotalSeconds)) >= requestTimeout)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"The remote learning timeout must be shorter than the configured request timeout of {requestTimeout.TotalSeconds:g} seconds.");
        }
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
        return state is null ? null : HomeAssistantRemoteStatus.FromState(state);
    }
}
