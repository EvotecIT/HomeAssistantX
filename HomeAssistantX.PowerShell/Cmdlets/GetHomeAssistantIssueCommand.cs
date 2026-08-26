using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Operations;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets Core repairs issues or Supervisor resolution issues.</summary>
/// <example>
///   <summary>List active Core Repairs issues</summary>
///   <code>$ha | Get-HomeAssistantIssue</code>
///   <para>Returns non-ignored Core Repairs issues by default.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantIssue", DefaultParameterSetName = CoreParameterSet)]
[OutputType(typeof(HomeAssistantRepairIssue))]
[OutputType(typeof(JsonElement))]
public sealed class GetHomeAssistantIssueCommand : HomeAssistantCmdlet
{
    private const string CoreParameterSet = "Core";
    private const string SupervisorParameterSet = "Supervisor";

    /// <summary>Returns Core Repairs issues. This is the default source.</summary>
    [Parameter(ParameterSetName = CoreParameterSet)]
    public SwitchParameter Core { get; set; }

    /// <summary>Returns Supervisor resolution issues.</summary>
    [Parameter(Mandatory = true, ParameterSetName = SupervisorParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Supervisor { get; set; }

    /// <summary>Includes Repairs issues currently marked as ignored.</summary>
    [Parameter(ParameterSetName = CoreParameterSet)]
    public SwitchParameter IncludeIgnored { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == SupervisorParameterSet)
        {
            var resolution = await Client.Supervisor.GetResolutionAsync(CancelToken).ConfigureAwait(false);
            if (resolution.ValueKind == JsonValueKind.Object
                && resolution.TryGetProperty("issues", out var issues)
                && issues.ValueKind == JsonValueKind.Array)
            {
                foreach (var issue in issues.EnumerateArray())
                {
                    WriteObject(issue.Clone());
                }
            }

            return;
        }

        WriteObject(
            await Client.Operations.Repairs.GetIssuesAsync(IncludeIgnored, CancelToken).ConfigureAwait(false),
            enumerateCollection: true);
    }
}
