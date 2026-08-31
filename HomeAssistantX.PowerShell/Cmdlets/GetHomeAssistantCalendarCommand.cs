using System.Management.Automation;
using HomeAssistantX.Rest;

namespace HomeAssistantX.PowerShell;

/// <summary>Lists Home Assistant calendar entities.</summary>
/// <example><summary>List available calendars</summary><code>Get-HomeAssistantCalendar</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantCalendar")]
[OutputType(typeof(HomeAssistantCalendar))]
public sealed class GetHomeAssistantCalendarCommand : HomeAssistantCmdlet
{
    protected override async Task ProcessRecordAsync()
    {
        WriteObject(await Client.Calendars.GetAsync(CancelToken).ConfigureAwait(false), true);
    }
}
