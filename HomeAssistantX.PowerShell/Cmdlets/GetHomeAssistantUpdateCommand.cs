using System.Management.Automation;
using HomeAssistantX.Operations;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets update entities or Supervisor component and app updates.</summary>
/// <example>
///   <summary>List available update entities</summary>
///   <code>$ha | Get-HomeAssistantUpdate -AvailableOnly</code>
///   <para>Returns update entities that currently advertise a newer version.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantUpdate", DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantUpdate))]
[OutputType(typeof(HomeAssistantSupervisorUpdate))]
public sealed class GetHomeAssistantUpdateCommand : HomeAssistantCmdlet
{
    private const string EntityParameterSet = "Entity";
    private const string SupervisorParameterSet = "Supervisor";

    /// <summary>Returns Home Assistant <c>update</c> entities. This is the default source.</summary>
    [Parameter(ParameterSetName = EntityParameterSet)]
    public SwitchParameter Entity { get; set; }

    /// <summary>Returns Supervisor component and app updates.</summary>
    [Parameter(Mandatory = true, ParameterSetName = SupervisorParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Supervisor { get; set; }

    /// <summary>Limits entity results to updates that are currently available.</summary>
    [Parameter(ParameterSetName = EntityParameterSet)]
    public SwitchParameter AvailableOnly { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var values = ParameterSetName == SupervisorParameterSet
            ? (object)await Client.Supervisor.GetAvailableUpdatesAsync(CancelToken).ConfigureAwait(false)
            : await Client.Operations.Updates.GetAllAsync(AvailableOnly, CancelToken).ConfigureAwait(false);
        WriteObject(values, enumerateCollection: true);
    }
}
