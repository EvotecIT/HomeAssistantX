using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;

namespace HomeAssistantX.Calendars;

/// <summary>Describes a timed or all-day calendar event to create or update.</summary>
public sealed class HomeAssistantCalendarEventInput
{
    private HomeAssistantCalendarEventInput(string start, string end, string summary)
    {
        Start = start;
        End = end;
        Summary = Require(summary, nameof(summary));
    }

    public string Start { get; }

    public string End { get; }

    public string Summary { get; }

    public string? Description { get; set; }

    public string? Location { get; set; }

    public string? RecurrenceRule
    {
        get => _recurrenceRule;
        set
        {
            if (value is not null)
            {
                ValidateRecurrenceRule(value, IsAllDay);
            }

            _recurrenceRule = value;
        }
    }

    private string? _recurrenceRule;

    public bool IsAllDay { get; private set; }

    public static HomeAssistantCalendarEventInput Timed(DateTimeOffset start, DateTimeOffset end, string summary)
    {
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), "The event end must be after its start.");
        }
        return new HomeAssistantCalendarEventInput(start.ToString("O", CultureInfo.InvariantCulture), end.ToString("O", CultureInfo.InvariantCulture), summary);
    }

    /// <summary>Creates an all-day event. The end date is exclusive, matching Home Assistant calendar semantics.</summary>
    public static HomeAssistantCalendarEventInput AllDay(string startDate, string endDate, string summary)
    {
        var start = ParseDate(startDate, nameof(startDate));
        var end = ParseDate(endDate, nameof(endDate));
        if (end <= start)
        {
            throw new ArgumentOutOfRangeException(nameof(endDate), "The all-day event end date must be after its start date.");
        }

        return new HomeAssistantCalendarEventInput(
            start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            summary)
        {
            IsAllDay = true
        };
    }

    internal IReadOnlyDictionary<string, object?> ToPayload()
    {
        var payload = new Dictionary<string, object?>
        {
            ["start"] = Start,
            ["end"] = End,
            ["summary"] = Summary
        };
        AddOptional(payload, "description", Description);
        AddOptional(payload, "location", Location);
        AddOptional(payload, "rrule", RecurrenceRule);
        return payload;
    }

    private static DateTime ParseDate(string value, string parameterName)
    {
        if (!DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw new ArgumentException("A calendar date must use yyyy-MM-dd format.", parameterName);
        }

        return date;
    }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value;
    }

    private static void AddOptional(IDictionary<string, object?> payload, string name, string? value)
    {
        if (value is not null)
        {
            payload[name] = value;
        }
    }

    private static void ValidateRecurrenceRule(string value, bool isAllDay)
    {
        HomeAssistantRecurrenceRuleValidator.Validate(value, isAllDay, nameof(value));
    }
}

/// <summary>Identifies an event or recurring occurrence to update or delete.</summary>
public sealed class HomeAssistantCalendarEventReference
{
    public HomeAssistantCalendarEventReference(string uid)
    {
        Uid = string.IsNullOrWhiteSpace(uid) ? throw new ArgumentException("An event UID is required.", nameof(uid)) : uid;
    }

    public string Uid { get; }

    public string? RecurrenceId { get; set; }

    public string? RecurrenceRange { get; set; }

    /// <summary>Validates recurrence targeting before an update or delete is dispatched.</summary>
    public void Validate() => Validate(default);

    internal void Validate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (RecurrenceId is not null && CancellationAwareString.IsNullOrWhiteSpace(RecurrenceId, cancellationToken))
            throw new ArgumentException("A supplied recurrence identifier cannot be empty.", nameof(RecurrenceId));
        if (RecurrenceRange is null) return;
        if (CancellationAwareString.IsNullOrWhiteSpace(RecurrenceId, cancellationToken))
            throw new ArgumentException("RecurrenceRange requires RecurrenceId.", nameof(RecurrenceRange));
        if (!CancellationAwareString.EqualsOrdinalIgnoreCase(RecurrenceRange, "THISANDFUTURE", cancellationToken))
            throw new ArgumentException("The supported recurrence range is THISANDFUTURE.", nameof(RecurrenceRange));
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal void AddTo(IDictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        Validate(cancellationToken);
        payload["uid"] = Uid;
        cancellationToken.ThrowIfCancellationRequested();
        if (RecurrenceId is not null)
        {
            payload["recurrence_id"] = RecurrenceId;
        }

        if (RecurrenceRange is not null)
        {
            payload["recurrence_range"] = "THISANDFUTURE";
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}

/// <summary>A live calendar event-list update. Unavailable updates represent a provider refresh failure.</summary>
public sealed class HomeAssistantCalendarEventUpdate
{
    public bool IsAvailable { get; set; }

    public IReadOnlyList<HomeAssistantCalendarEvent> Events { get; set; } = Array.Empty<HomeAssistantCalendarEvent>();

    public JsonElement Raw { get; set; }
}
