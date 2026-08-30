using System.Management.Automation;
using HomeAssistantX.Calendars;
using HomeAssistantX.Models;

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
        CancelToken.ThrowIfCancellationRequested();
        if (!HomeAssistantEntityId.TryNormalizeForDomain(
                EntityId,
                "calendar",
                CancelToken,
                out var entityId))
        {
            throw new ArgumentException("A calendar entity identifier is required.", nameof(EntityId));
        }

        var reference = new HomeAssistantCalendarEventReference(Uid)
        {
            RecurrenceId = RecurrenceId,
            RecurrenceRange = RecurrenceRange
        };
        reference.Validate(CancelToken);
        if (ShouldProcess(entityId + "/" + Uid, "Delete Home Assistant calendar event"))
        {
            await Client.Calendars.DeleteEventAsync(entityId, reference, CancelToken).ConfigureAwait(false);
        }
    }
}
