using System.Management.Automation;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets all Supervisor jobs or one job by identifier.</summary>
/// <example>
///   <summary>Inspect one Supervisor job</summary>
///   <code>$ha | Get-HomeAssistantJob -Id 'job-id'</code>
///   <para>Returns progress and completion metadata for one job.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantJob", DefaultParameterSetName = AllParameterSet)]
[OutputType(typeof(HomeAssistantSupervisorJob))]
public sealed class GetHomeAssistantJobCommand : HomeAssistantCmdlet
{
    private const string AllParameterSet = "All";
    private const string IdParameterSet = "Id";

    /// <summary>Returns recent Supervisor jobs. This is the default behavior.</summary>
    [Parameter(ParameterSetName = AllParameterSet)]
    public SwitchParameter All { get; set; }

    /// <summary>Exact Supervisor job identifier.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = IdParameterSet)]
    [ValidateNotNullOrEmpty]
    public string Id { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == IdParameterSet)
        {
            WriteObject(await Client.Supervisor.GetJobAsync(Id, CancelToken).ConfigureAwait(false));
            return;
        }

        WriteObject(await Client.Supervisor.GetJobsAsync(CancelToken).ConfigureAwait(false), enumerateCollection: true);
    }
}
