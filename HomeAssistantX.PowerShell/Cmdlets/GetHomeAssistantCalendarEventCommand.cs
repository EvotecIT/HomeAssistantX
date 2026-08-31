using System.Management.Automation;
using HomeAssistantX.Rest;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets events from one Home Assistant calendar over an explicit time range.</summary>
/// <example><summary>Read the next seven days</summary><code>Get-HomeAssistantCalendarEvent -EntityId calendar.home -EndTime (Get-Date).AddDays(7)</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantCalendarEvent")]
[OutputType(typeof(HomeAssistantCalendarEvent))]
public sealed class GetHomeAssistantCalendarEventCommand : HomeAssistantCmdlet
{
    /// <summary>Calendar entity identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Range start. Defaults to the current instant.</summary>
    [Parameter]
    public DateTimeOffset? StartTime { get; set; }

    /// <summary>Range end. Defaults to 30 days after the start.</summary>
    [Parameter]
    public DateTimeOffset? EndTime { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var start = StartTime ?? DateTimeOffset.Now;
        var end = EndTime ?? start.AddDays(30);
        WriteObject(await Client.Calendars.GetEventsAsync(EntityId, start, end, CancelToken).ConfigureAwait(false), true);
    }
}
