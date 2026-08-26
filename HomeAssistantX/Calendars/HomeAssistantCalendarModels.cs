using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
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
                ValidateRecurrenceRule(value);
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
        if (start.Offset != end.Offset)
        {
            throw new ArgumentException("Timed event boundaries must use the same UTC offset.", nameof(end));
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

    private static void ValidateRecurrenceRule(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A recurrence rule cannot be empty.", nameof(value));
        }

        var frequency = value.Split(';')
            .Select(part => part.Split(new[] { '=' }, 2))
            .FirstOrDefault(part => part.Length == 2 && string.Equals(part[0], "FREQ", StringComparison.Ordinal));
        if (frequency is null || !(new[] { "DAILY", "WEEKLY", "MONTHLY", "YEARLY" }).Contains(frequency[1], StringComparer.Ordinal))
        {
            throw new ArgumentException("The recurrence rule must contain a supported FREQ value: DAILY, WEEKLY, MONTHLY, or YEARLY.", nameof(value));
        }
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

    internal void AddTo(IDictionary<string, object?> payload)
    {
        payload["uid"] = Uid;
        if (RecurrenceId is not null)
        {
            payload["recurrence_id"] = RecurrenceId;
        }

        if (RecurrenceRange is not null)
        {
            payload["recurrence_range"] = RecurrenceRange;
        }
    }
}

/// <summary>A live calendar event-list update. Unavailable updates represent a provider refresh failure.</summary>
public sealed class HomeAssistantCalendarEventUpdate
{
    public bool IsAvailable { get; set; }

    public IReadOnlyList<HomeAssistantCalendarEvent> Events { get; set; } = Array.Empty<HomeAssistantCalendarEvent>();

    public JsonElement Raw { get; set; }
}
