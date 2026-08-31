using System.Management.Automation;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using System.Text.Json;
using HomeAssistantX.Services;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Installs an update entity or a Supervisor-managed Core, OS, Supervisor, or app update.</summary>
/// <example>
///   <summary>Preview a Core update</summary>
///   <code>$ha | Install-HomeAssistantUpdate -Core -WhatIf</code>
///   <para>Shows the high-impact operation without installing the update.</para>
/// </example>
[Cmdlet(VerbsLifecycle.Install, "HomeAssistantUpdate", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
[OutputType(typeof(JsonElement))]
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
    [ValidateSwitchPresent]
    public SwitchParameter Core { get; set; }

    /// <summary>Installs a Supervisor update.</summary>
    [Parameter(Mandatory = true, ParameterSetName = SupervisorParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Supervisor { get; set; }

    /// <summary>Installs a Home Assistant OS update.</summary>
    [Parameter(Mandatory = true, ParameterSetName = OperatingSystemParameterSet)]
    [ValidateSwitchPresent]
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
        var version = NormalizeOptionalVersion(Version, CancelToken);
        object? result;
        if (ParameterSetName == EntityParameterSet)
        {
            var entityId = NormalizeUpdateEntityId(EntityId, CancelToken);
            if (!ShouldProcess(entityId, "Install Home Assistant update"))
            {
                return;
            }

            result = await Client.Operations.Updates.InstallAsync(entityId, version, Backup, CancelToken).ConfigureAwait(false);
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
            var app = target == HomeAssistantSupervisorUpdateTarget.App
                ? NormalizeSupervisorApp(App, CancelToken)
                : App;
            var description = target == HomeAssistantSupervisorUpdateTarget.App ? "app " + app : target.ToString();
            if (!ShouldProcess(description, "Install Home Assistant update"))
            {
                return;
            }

            result = await Client.Supervisor.InstallUpdateAsync(target, app, version, Backup, CancelToken).ConfigureAwait(false);
        }

        if (PassThru)
        {
            WriteObject(result);
        }
    }

    private static string NormalizeUpdateEntityId(string value, CancellationToken cancellationToken)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(value, "update", cancellationToken, out var normalized))
        {
            throw new ArgumentException("An update entity identifier is required.", nameof(EntityId));
        }

        return normalized;
    }

    private static string? NormalizeOptionalVersion(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null)
        {
            return null;
        }

        if (CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken))
        {
            throw new ArgumentException("A supplied update version cannot be empty.", nameof(Version));
        }

        return CancellationAwareString.Trim(value, cancellationToken);
    }

    private static string NormalizeSupervisorApp(string value, CancellationToken cancellationToken)
    {
        if (!HomeAssistantSupervisorIdentifier.TryNormalizeAppSlug(value, cancellationToken, out var normalized))
        {
            throw new ArgumentException("A valid Supervisor app/add-on slug is required.", nameof(App));
        }

        return normalized;
    }
}
