using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Remote operations exposed as one task-oriented PowerShell action.</summary>
public enum HomeAssistantRemoteAction
{
    TurnOn,
    TurnOff,
    Toggle,
    SendCommand,
    LearnCommand,
    DeleteCommand
}

/// <summary>Controls a Home Assistant remote, including sending, learning, and deleting commands.</summary>
/// <example>
///   <summary>Send a power command twice</summary>
///   <code>Invoke-HomeAssistantRemote -Entity remote.living_room -Action SendCommand -Command Power -RepeatCount 2 -WhatIf</code>
/// </example>
/// <example>
///   <summary>Start an activity</summary>
///   <code>Invoke-HomeAssistantRemote -Entity remote.harmony -Action TurnOn -Activity 'Watch TV'</code>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HomeAssistantRemote", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class InvokeHomeAssistantRemoteCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Remote operation to perform.</summary>
    [Parameter(Mandatory = true)]
    public HomeAssistantRemoteAction Action { get; set; }

    /// <summary>Activity passed to a remote power operation.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Activity { get; set; }

    /// <summary>One or more commands to send, learn, or delete.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? Command { get; set; }

    /// <summary>Optional receiver or device known by the remote integration.</summary>
    [Parameter]
    [Alias("DeviceName")]
    [ValidateNotNullOrEmpty]
    public string? RemoteDevice { get; set; }

    /// <summary>Number of times each sent command is repeated.</summary>
    [Parameter]
    [ValidateRange(1, int.MaxValue)]
    public int? RepeatCount { get; set; }

    /// <summary>Delay between repeated sent commands, in seconds.</summary>
    [Parameter]
    public double? DelaySeconds { get; set; }

    /// <summary>Duration for which a sent command is held, in seconds.</summary>
    [Parameter]
    public double? HoldSeconds { get; set; }

    /// <summary>IR or RF command type used while learning.</summary>
    [Parameter]
    public HomeAssistantRemoteCommandType? CommandType { get; set; }

    /// <summary>Requests the integration's alternative learning mode.</summary>
    [Parameter]
    public bool? Alternative { get; set; }

    /// <summary>Learning timeout in seconds.</summary>
    [Parameter]
    public double? TimeoutSeconds { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantRemoteAction), Action))
        {
            throw new ArgumentOutOfRangeException(nameof(Action), Action, "Unsupported remote action.");
        }

        if (CommandType.HasValue && !Enum.IsDefined(typeof(HomeAssistantRemoteCommandType), CommandType.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(CommandType), CommandType.Value, "Unsupported remote command type.");
        }

        ValidateFiniteDuration(DelaySeconds, nameof(DelaySeconds), allowZero: true);
        ValidateFiniteDuration(HoldSeconds, nameof(HoldSeconds), allowZero: true);
        ValidateFiniteDuration(TimeoutSeconds, nameof(TimeoutSeconds), allowZero: false);
        ValidateShape();

        var target = await ResolveTargetAsync("remote").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, Action.ToString()))
        {
            return;
        }

        var result = Action switch
        {
            HomeAssistantRemoteAction.TurnOn => await Client.Controls.Remotes.SetPowerAsync(
                target.Target, HomeAssistantPowerAction.On, Activity, CancelToken).ConfigureAwait(false),
            HomeAssistantRemoteAction.TurnOff => await Client.Controls.Remotes.SetPowerAsync(
                target.Target, HomeAssistantPowerAction.Off, Activity, CancelToken).ConfigureAwait(false),
            HomeAssistantRemoteAction.Toggle => await Client.Controls.Remotes.SetPowerAsync(
                target.Target, HomeAssistantPowerAction.Toggle, Activity, CancelToken).ConfigureAwait(false),
            HomeAssistantRemoteAction.SendCommand => await Client.Controls.Remotes.SendCommandsAsync(
                target.Target,
                Command!,
                new HomeAssistantRemoteSendOptions
                {
                    Device = RemoteDevice,
                    RepeatCount = RepeatCount,
                    Delay = ToDuration(DelaySeconds),
                    Hold = ToDuration(HoldSeconds)
                },
                CancelToken).ConfigureAwait(false),
            HomeAssistantRemoteAction.LearnCommand => await Client.Controls.Remotes.LearnCommandsAsync(
                target.Target,
                new HomeAssistantRemoteLearnOptions
                {
                    Device = RemoteDevice,
                    Commands = Command,
                    CommandType = CommandType,
                    Alternative = Alternative,
                    Timeout = ToDuration(TimeoutSeconds)
                },
                CancelToken).ConfigureAwait(false),
            HomeAssistantRemoteAction.DeleteCommand => await Client.Controls.Remotes.DeleteCommandsAsync(
                target.Target, Command!, RemoteDevice, CancelToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(Action), Action, "Unsupported remote action.")
        };
        WriteObject(result);
    }

    private void ValidateShape()
    {
        var hasCommands = Command is not null;
        if (hasCommands && (Command!.Length == 0 || Command.Any(string.IsNullOrWhiteSpace)))
        {
            throw new ArgumentException("Command must contain at least one non-empty value.", nameof(Command));
        }

        var hasSendValues = RepeatCount.HasValue || DelaySeconds.HasValue || HoldSeconds.HasValue;
        var hasLearnValues = CommandType.HasValue || Alternative.HasValue || TimeoutSeconds.HasValue;
        switch (Action)
        {
            case HomeAssistantRemoteAction.TurnOn:
            case HomeAssistantRemoteAction.TurnOff:
            case HomeAssistantRemoteAction.Toggle:
                if (hasCommands || !string.IsNullOrWhiteSpace(RemoteDevice) || hasSendValues || hasLearnValues)
                {
                    throw new ArgumentException("Power actions accept only the optional Activity value.");
                }

                break;
            case HomeAssistantRemoteAction.SendCommand:
                if (!hasCommands)
                {
                    throw new ArgumentException("SendCommand requires Command.", nameof(Command));
                }

                if (!string.IsNullOrWhiteSpace(Activity) || hasLearnValues)
                {
                    throw new ArgumentException("SendCommand does not accept Activity or learning options.");
                }

                break;
            case HomeAssistantRemoteAction.LearnCommand:
                if (!string.IsNullOrWhiteSpace(Activity) || hasSendValues)
                {
                    throw new ArgumentException("LearnCommand does not accept Activity or send timing options.");
                }

                break;
            case HomeAssistantRemoteAction.DeleteCommand:
                if (!hasCommands)
                {
                    throw new ArgumentException("DeleteCommand requires Command.", nameof(Command));
                }

                if (!string.IsNullOrWhiteSpace(Activity) || hasSendValues || hasLearnValues)
                {
                    throw new ArgumentException("DeleteCommand accepts only Command and RemoteDevice.");
                }

                break;
        }
    }

    private static TimeSpan? ToDuration(double? seconds)
    {
        return seconds.HasValue ? TimeSpan.FromSeconds(seconds.Value) : null;
    }

    private static void ValidateFiniteDuration(double? value, string name, bool allowZero)
    {
        if (!value.HasValue)
        {
            return;
        }

        var minimum = allowZero ? 0d : double.Epsilon;
        if (double.IsNaN(value.Value)
            || double.IsInfinity(value.Value)
            || value.Value < minimum
            || value.Value > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(name, $"The value must be a finite number of seconds between {(allowZero ? "zero" : "greater than zero")} and {int.MaxValue}.");
        }
    }
}
