using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets Core configuration, an operational snapshot, capabilities, system health, or Supervisor information.</summary>
/// <example>
///   <summary>Discover capabilities for the current connection</summary>
///   <code>$ha | Get-HomeAssistantInfo -Capabilities</code>
///   <para>Reports installed and permission-dependent operational capabilities without changing Home Assistant.</para>
/// </example>
/// <example>
///   <summary>Summarize operational health without exposing entity names or log messages</summary>
///   <code>$ha | Get-HomeAssistantInfo -OperationalSnapshot</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantInfo", DefaultParameterSetName = OverviewParameterSet)]
[OutputType(typeof(Models.HomeAssistantConfiguration))]
[OutputType(typeof(Operations.HomeAssistantCapabilityReport))]
[OutputType(typeof(Operations.HomeAssistantOperationalSnapshot))]
[OutputType(typeof(Operations.HomeAssistantSystemHealthSnapshot))]
[OutputType(typeof(Supervisor.HomeAssistantSupervisorOverview))]
public sealed class GetHomeAssistantInfoCommand : HomeAssistantCmdlet
{
    private const string OverviewParameterSet = "Overview";
    private const string CapabilitiesParameterSet = "Capabilities";
    private const string OperationalSnapshotParameterSet = "OperationalSnapshot";
    private const string HealthParameterSet = "Health";
    private const string SupervisorParameterSet = "Supervisor";

    /// <summary>Returns Core configuration and version information. This is the default view.</summary>
    [Parameter(ParameterSetName = OverviewParameterSet)]
    public SwitchParameter Overview { get; set; }

    /// <summary>Returns discovered operational capabilities and their availability.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CapabilitiesParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Capabilities { get; set; }

    /// <summary>Returns bounded counts and capability availability with explicit partial-read state.</summary>
    [Parameter(Mandatory = true, ParameterSetName = OperationalSnapshotParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter OperationalSnapshot { get; set; }

    /// <summary>Returns the streamed Core system-health snapshot.</summary>
    [Parameter(Mandatory = true, ParameterSetName = HealthParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Health { get; set; }

    /// <summary>Returns Supervisor and Home Assistant OS information when available.</summary>
    [Parameter(Mandatory = true, ParameterSetName = SupervisorParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Supervisor { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        object result = ParameterSetName switch
        {
            CapabilitiesParameterSet => await Client.Operations.GetCapabilitiesAsync(CancelToken).ConfigureAwait(false),
            OperationalSnapshotParameterSet => await Client.Operations.GetOperationalSnapshotAsync(CancelToken).ConfigureAwait(false),
            HealthParameterSet => await Client.Operations.Health.GetAsync(CancelToken).ConfigureAwait(false),
            SupervisorParameterSet => await Client.Supervisor.GetOverviewAsync(CancelToken).ConfigureAwait(false),
            _ => await Client.Rest.GetConfigurationAsync(CancelToken).ConfigureAwait(false)
        };
        WriteObject(result);
    }
}
