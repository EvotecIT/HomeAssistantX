using System.Management.Automation;
using HomeAssistantX.Models;
using HomeAssistantX.Rest;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets recorder history for one or more entity identifiers.</summary>
/// <example>
///   <summary>Read one hour of compact history</summary>
///   <code>$ha | Get-HomeAssistantHistory -EntityId 'sensor.temperature' -StartTime (Get-Date).AddHours(-1) -MinimalResponse</code>
///   <para>Returns recorder history without polling the current state endpoint.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantHistory")]
[OutputType(typeof(HomeAssistantState))]
public sealed class GetHomeAssistantHistoryCommand : HomeAssistantCmdlet
{
    /// <summary>Entity identifiers whose state history should be returned.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string[] EntityId { get; set; } = Array.Empty<string>();

    /// <summary>Inclusive history start time. Defaults to Home Assistant's endpoint behavior.</summary>
    [Parameter]
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>Exclusive history end time.</summary>
    [Parameter]
    public DateTimeOffset? EndTime { get; set; }

    /// <summary>Requests Home Assistant's compact history representation.</summary>
    [Parameter]
    public SwitchParameter MinimalResponse { get; set; }

    /// <summary>Omits state attributes from the history response.</summary>
    [Parameter]
    public SwitchParameter NoAttributes { get; set; }

    /// <summary>Requests only significant state changes where supported.</summary>
    [Parameter]
    public SwitchParameter SignificantChangesOnly { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (EndTime.HasValue && !StartTime.HasValue)
            throw new ArgumentException("EndTime requires StartTime so the requested history window is explicit.", nameof(EndTime));
        if (StartTime.HasValue && EndTime.HasValue && EndTime <= StartTime)
            throw new ArgumentOutOfRangeException(nameof(EndTime), "EndTime must be after StartTime.");
        var query = new HomeAssistantHistoryQuery(EntityId)
        {
            StartTime = StartTime,
            EndTime = EndTime,
            MinimalResponse = MinimalResponse,
            NoAttributes = NoAttributes,
            SignificantChangesOnly = SignificantChangesOnly
        };
        var groups = await Client.Rest.GetHistoryAsync(query, CancelToken).ConfigureAwait(false);
        foreach (var group in groups)
        {
            WriteObject(group, enumerateCollection: true);
        }
    }
}
