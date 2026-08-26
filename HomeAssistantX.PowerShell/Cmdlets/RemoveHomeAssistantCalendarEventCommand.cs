using System.Management.Automation;
using HomeAssistantX.Calendars;

namespace HomeAssistantX.PowerShell;

/// <summary>Deletes a Home Assistant calendar event or recurring occurrence.</summary>
/// <example><summary>Preview deleting one event</summary><code>Remove-HomeAssistantCalendarEvent -EntityId calendar.home -Uid event-1 -WhatIf</code></example>
[Cmdlet(VerbsCommon.Remove, "HomeAssistantCalendarEvent", SupportsShouldProcess = true)]
public sealed class RemoveHomeAssistantCalendarEventCommand : HomeAssistantCmdlet
{
    /// <summary>Calendar entity identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Native event UID.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string Uid { get; set; } = string.Empty;

    /// <summary>Optional recurring occurrence identifier.</summary>
    [Parameter]
    public string? RecurrenceId { get; set; }

    /// <summary>Optional provider recurrence range, such as THISANDFUTURE.</summary>
    [Parameter]
    public string? RecurrenceRange { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var reference = new HomeAssistantCalendarEventReference(Uid)
        {
            RecurrenceId = RecurrenceId,
            RecurrenceRange = RecurrenceRange
        };
        if (ShouldProcess(EntityId + "/" + Uid, "Delete Home Assistant calendar event"))
        {
            await Client.Calendars.DeleteEventAsync(EntityId, reference, CancelToken).ConfigureAwait(false);
        }
    }
}
