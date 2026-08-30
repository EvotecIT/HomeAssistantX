using System.Management.Automation;
using HomeAssistantX.Calendars;
using HomeAssistantX.Models;

namespace HomeAssistantX.PowerShell;

/// <summary>Creates or updates a timed or all-day Home Assistant calendar event.</summary>
/// <example><summary>Create a timed event</summary><code>Set-HomeAssistantCalendarEvent -EntityId calendar.home -Summary Dinner -StartTime '2026-08-27T18:00:00+02:00' -EndTime '2026-08-27T20:00:00+02:00' -WhatIf</code></example>
/// <example><summary>Update one recurring occurrence</summary><code>Set-HomeAssistantCalendarEvent -EntityId calendar.home -Uid event-1 -RecurrenceId 20260827 -Summary Dinner -StartDate 2026-08-27 -EndDate 2026-08-28</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantCalendarEvent", SupportsShouldProcess = true, DefaultParameterSetName = CreateTimed)]
public sealed class SetHomeAssistantCalendarEventCommand : HomeAssistantCmdlet
{
    private const string CreateTimed = "CreateTimed";
    private const string CreateAllDay = "CreateAllDay";
    private const string UpdateTimed = "UpdateTimed";
    private const string UpdateAllDay = "UpdateAllDay";

    /// <summary>Calendar entity identifier.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string EntityId { get; set; } = string.Empty;

    /// <summary>Event summary or title.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Summary { get; set; } = string.Empty;

    /// <summary>Timed-event start including an offset.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CreateTimed)]
    [Parameter(Mandatory = true, ParameterSetName = UpdateTimed)]
    public DateTimeOffset StartTime { get; set; }

    /// <summary>Timed-event end including an offset.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CreateTimed)]
    [Parameter(Mandatory = true, ParameterSetName = UpdateTimed)]
    public DateTimeOffset EndTime { get; set; }

    /// <summary>All-day start date in yyyy-MM-dd form.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CreateAllDay)]
    [Parameter(Mandatory = true, ParameterSetName = UpdateAllDay)]
    [ValidatePattern("^\\d{4}-\\d{2}-\\d{2}$")]
    public string StartDate { get; set; } = string.Empty;

    /// <summary>Exclusive all-day end date in yyyy-MM-dd form.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CreateAllDay)]
    [Parameter(Mandatory = true, ParameterSetName = UpdateAllDay)]
    [ValidatePattern("^\\d{4}-\\d{2}-\\d{2}$")]
    public string EndDate { get; set; } = string.Empty;

    /// <summary>Existing event UID; supplying it selects an update parameter set.</summary>
    [Parameter(Mandatory = true, ParameterSetName = UpdateTimed)]
    [Parameter(Mandatory = true, ParameterSetName = UpdateAllDay)]
    [ValidateNotNullOrEmpty]
    public string Uid { get; set; } = string.Empty;

    /// <summary>Optional recurring occurrence identifier.</summary>
    [Parameter(ParameterSetName = UpdateTimed)]
    [Parameter(ParameterSetName = UpdateAllDay)]
    public string? RecurrenceId { get; set; }

    /// <summary>Optional provider recurrence range, such as THISANDFUTURE.</summary>
    [Parameter(ParameterSetName = UpdateTimed)]
    [Parameter(ParameterSetName = UpdateAllDay)]
    public string? RecurrenceRange { get; set; }

    /// <summary>Optional event description.</summary>
    [Parameter]
    public string? Description { get; set; }

    /// <summary>Optional event location.</summary>
    [Parameter]
    public string? Location { get; set; }

    /// <summary>Optional iCalendar recurrence rule, such as FREQ=WEEKLY.</summary>
    [Parameter]
    public string? RecurrenceRule { get; set; }

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

        var allDay = ParameterSetName == CreateAllDay || ParameterSetName == UpdateAllDay;
        var update = ParameterSetName == UpdateTimed || ParameterSetName == UpdateAllDay;
        var input = allDay
            ? HomeAssistantCalendarEventInput.AllDay(StartDate, EndDate, Summary)
            : HomeAssistantCalendarEventInput.Timed(StartTime, EndTime, Summary);
        input.Description = Description;
        input.Location = Location;
        input.SetRecurrenceRule(RecurrenceRule, CancelToken);

        HomeAssistantCalendarEventReference? reference = null;
        if (update)
        {
            reference = new HomeAssistantCalendarEventReference(Uid)
            {
                RecurrenceId = RecurrenceId,
                RecurrenceRange = RecurrenceRange
            };
            reference.Validate(CancelToken);
        }

        var action = update ? "Update Home Assistant calendar event" : "Create Home Assistant calendar event";
        if (!ShouldProcess(entityId + (update ? "/" + Uid : string.Empty), action))
        {
            return;
        }

        if (!update)
        {
            await Client.Calendars.CreateEventAsync(entityId, input, CancelToken).ConfigureAwait(false);
            return;
        }

        await Client.Calendars.UpdateEventAsync(entityId, reference!, input, CancelToken).ConfigureAwait(false);
    }
}
