using System.Management.Automation;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Restarts Core, Supervisor, host, an app, or reloads one integration.</summary>
/// <example>
///   <summary>Preview restarting Core</summary>
///   <code>$ha | Restart-HomeAssistant -Core -WhatIf</code>
///   <para>Shows the restart target and requires no change while WhatIf is present.</para>
/// </example>
[Cmdlet(VerbsLifecycle.Restart, "HomeAssistant", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = CoreParameterSet)]
public sealed class RestartHomeAssistantCommand : HomeAssistantCmdlet
{
    private const string CoreParameterSet = "Core";
    private const string SupervisorParameterSet = "Supervisor";
    private const string HostParameterSet = "Host";
    private const string AppParameterSet = "App";
    private const string IntegrationParameterSet = "Integration";

    /// <summary>Restarts Home Assistant Core. This is the default target.</summary>
    [Parameter(ParameterSetName = CoreParameterSet)]
    public SwitchParameter Core { get; set; }

    /// <summary>Restarts Supervisor.</summary>
    [Parameter(Mandatory = true, ParameterSetName = SupervisorParameterSet)]
    public SwitchParameter Supervisor { get; set; }

    /// <summary>Reboots the Home Assistant host system. <c>Host</c> is an alias.</summary>
    [Parameter(Mandatory = true, ParameterSetName = HostParameterSet)]
    [Alias("Host")]
    public SwitchParameter HostSystem { get; set; }

    /// <summary>Restarts the specified Supervisor app/add-on.</summary>
    [Parameter(Mandatory = true, ParameterSetName = AppParameterSet)]
    [ValidateNotNullOrEmpty]
    public string App { get; set; } = string.Empty;

    /// <summary>Reloads the specified Home Assistant configuration entry.</summary>
    [Parameter(Mandatory = true, ParameterSetName = IntegrationParameterSet)]
    [ValidateNotNullOrEmpty]
    public string IntegrationId { get; set; } = string.Empty;

    /// <summary>Writes the operation result to the pipeline.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        object? result;
        string target;
        string action;
        switch (ParameterSetName)
        {
            case IntegrationParameterSet:
                target = "integration " + IntegrationId;
                action = "Reload";
                if (!ShouldProcess(target, action))
                {
                    return;
                }

                result = await Client.Operations.Integrations.ReloadAsync(IntegrationId, CancelToken).ConfigureAwait(false);
                break;
            case AppParameterSet:
                if (!HomeAssistantSupervisorIdentifier.TryNormalizeAppSlug(App, out var app))
                {
                    throw new ArgumentException("A valid Supervisor app/add-on slug is required.", nameof(App));
                }

                target = "app " + app;
                action = "Restart";
                if (!ShouldProcess(target, action))
                {
                    return;
                }

                result = await Client.Supervisor.InvokeAppAsync(app, HomeAssistantAppOperation.Restart, CancelToken).ConfigureAwait(false);
                break;
            default:
                var supervisorTarget = ParameterSetName switch
                {
                    SupervisorParameterSet => HomeAssistantSupervisorRestartTarget.Supervisor,
                    HostParameterSet => HomeAssistantSupervisorRestartTarget.Host,
                    _ => HomeAssistantSupervisorRestartTarget.Core
                };
                target = supervisorTarget + " on " + ActiveConnection;
                action = supervisorTarget == HomeAssistantSupervisorRestartTarget.Host ? "Reboot" : "Restart";
                if (!ShouldProcess(target, action))
                {
                    return;
                }

                result = await Client.Supervisor.RestartAsync(supervisorTarget, CancelToken).ConfigureAwait(false);
                break;
        }

        if (PassThru)
        {
            WriteObject(result);
        }
    }
}
