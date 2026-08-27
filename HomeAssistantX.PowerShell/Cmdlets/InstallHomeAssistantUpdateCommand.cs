using System.Management.Automation;
using HomeAssistantX.Models;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Installs an update entity or a Supervisor-managed Core, OS, Supervisor, or app update.</summary>
/// <example>
///   <summary>Preview a Core update</summary>
///   <code>$ha | Install-HomeAssistantUpdate -Core -WhatIf</code>
///   <para>Shows the high-impact operation without installing the update.</para>
/// </example>
[Cmdlet(VerbsLifecycle.Install, "HomeAssistantUpdate", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = EntityParameterSet)]
public sealed class InstallHomeAssistantUpdateCommand : HomeAssistantCmdlet
{
    private const string EntityParameterSet = "Entity";
    private const string CoreParameterSet = "Core";
    private const string SupervisorParameterSet = "Supervisor";
    private const string OperatingSystemParameterSet = "OperatingSystem";
    private const string AppParameterSet = "App";

    /// <summary>Home Assistant <c>update</c> entity to install.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = EntityParameterSet)]
    [ValidateNotNullOrEmpty]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Installs a Home Assistant Core update through Supervisor.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CoreParameterSet)]
    public SwitchParameter Core { get; set; }

    /// <summary>Installs a Supervisor update.</summary>
    [Parameter(Mandatory = true, ParameterSetName = SupervisorParameterSet)]
    public SwitchParameter Supervisor { get; set; }

    /// <summary>Installs a Home Assistant OS update.</summary>
    [Parameter(Mandatory = true, ParameterSetName = OperatingSystemParameterSet)]
    public SwitchParameter OperatingSystem { get; set; }

    /// <summary>Supervisor app/add-on slug to update.</summary>
    [Parameter(Mandatory = true, ParameterSetName = AppParameterSet)]
    [ValidateNotNullOrEmpty]
    public string App { get; set; } = string.Empty;

    /// <summary>Specific target version. Omit it to install the advertised latest version.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Version { get; set; }

    /// <summary>Requests a backup before installation when the target supports it.</summary>
    [Parameter]
    public bool? Backup { get; set; }

    /// <summary>Writes the action result to the pipeline.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        object? result;
        if (ParameterSetName == EntityParameterSet)
        {
            var entityId = NormalizeUpdateEntityId(EntityId);
            if (!ShouldProcess(entityId, "Install Home Assistant update"))
            {
                return;
            }

            result = await Client.Operations.Updates.InstallAsync(entityId, Version, Backup, CancelToken).ConfigureAwait(false);
        }
        else
        {
            var target = ParameterSetName switch
            {
                CoreParameterSet => HomeAssistantSupervisorUpdateTarget.Core,
                SupervisorParameterSet => HomeAssistantSupervisorUpdateTarget.Supervisor,
                OperatingSystemParameterSet => HomeAssistantSupervisorUpdateTarget.OperatingSystem,
                AppParameterSet => HomeAssistantSupervisorUpdateTarget.App,
                _ => throw new InvalidOperationException("Unexpected update parameter set.")
            };
            var description = target == HomeAssistantSupervisorUpdateTarget.App ? "app " + App : target.ToString();
            if (!ShouldProcess(description, "Install Home Assistant update"))
            {
                return;
            }

            result = await Client.Supervisor.InstallUpdateAsync(target, App, Version, Backup, CancelToken).ConfigureAwait(false);
        }

        if (PassThru)
        {
            WriteObject(result);
        }
    }

    private static string NormalizeUpdateEntityId(string value)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(value, "update", out var normalized))
        {
            throw new ArgumentException("An update entity identifier is required.", nameof(EntityId));
        }

        return normalized;
    }
}
