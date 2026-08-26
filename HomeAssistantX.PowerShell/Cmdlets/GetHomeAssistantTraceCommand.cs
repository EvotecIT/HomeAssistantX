using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Operations;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets automation or script trace summaries, or one complete trace run.</summary>
/// <example>
///   <summary>List recent automation traces</summary>
///   <code>$ha | Get-HomeAssistantTrace -Domain automation -ItemId 'morning_lights'</code>
///   <para>Returns trace summaries; add RunId to retrieve one complete run.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantTrace", DefaultParameterSetName = ListParameterSet)]
[OutputType(typeof(HomeAssistantTraceSummary))]
[OutputType(typeof(JsonElement))]
public sealed class GetHomeAssistantTraceCommand : HomeAssistantCmdlet
{
    private const string ListParameterSet = "List";
    private const string RunParameterSet = "Run";

    /// <summary>Trace domain: <c>automation</c> or <c>script</c>.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateSet("automation", "script")]
    public string Domain { get; set; } = string.Empty;

    /// <summary>Automation or script item identifier.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string ItemId { get; set; } = string.Empty;

    /// <summary>Exact trace run identifier. Omit it to list trace summaries.</summary>
    [Parameter(Mandatory = true, ParameterSetName = RunParameterSet)]
    [ValidateNotNullOrEmpty]
    public string RunId { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == RunParameterSet)
        {
            WriteObject(await Client.Operations.Traces.GetAsync(Domain, ItemId, RunId, CancelToken).ConfigureAwait(false));
            return;
        }

        WriteObject(
            await Client.Operations.Traces.GetAllAsync(Domain, ItemId, CancelToken).ConfigureAwait(false),
            enumerateCollection: true);
    }
}
