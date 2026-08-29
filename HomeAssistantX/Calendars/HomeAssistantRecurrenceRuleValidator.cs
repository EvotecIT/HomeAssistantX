using System.Globalization;

namespace HomeAssistantX.Calendars;

internal static class HomeAssistantRecurrenceRuleValidator
{
    private static readonly HashSet<string> SupportedFrequencies = new(StringComparer.Ordinal)
    {
        "DAILY", "WEEKLY", "MONTHLY", "YEARLY"
    };

    private static readonly HashSet<string> Weekdays = new(StringComparer.Ordinal)
    {
        "MO", "TU", "WE", "TH", "FR", "SA", "SU"
    };

    public static void Validate(string value, bool isAllDay, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(parameterName, "A recurrence rule cannot be empty.");
        }

        var clauses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var clause in value.Split(';'))
        {
            var separator = clause.IndexOf('=');
            if (separator <= 0 || separator != clause.LastIndexOf('=') || separator == clause.Length - 1)
            {
                throw Invalid(parameterName, "Every recurrence clause must use NAME=VALUE syntax.");
            }

            var name = clause.Substring(0, separator);
            var clauseValue = clause.Substring(separator + 1);
            if (clauses.ContainsKey(name))
            {
                throw Invalid(parameterName, $"The recurrence rule contains duplicate {name} clauses.");
            }

            clauses.Add(name, clauseValue);
        }

        if (!clauses.TryGetValue("FREQ", out var frequency)
            || !SupportedFrequencies.Contains(frequency))
        {
            throw Invalid(
                parameterName,
                "The recurrence rule must contain one supported FREQ value: DAILY, WEEKLY, MONTHLY, or YEARLY.");
        }

        foreach (var clause in clauses)
        {
            ValidateClause(clause.Key, clause.Value, isAllDay, parameterName);
        }

        if (clauses.ContainsKey("COUNT") && clauses.ContainsKey("UNTIL"))
        {
            throw Invalid(parameterName, "COUNT and UNTIL cannot be combined in one recurrence rule.");
        }

        if (clauses.ContainsKey("BYSETPOS")
            && !clauses.Keys.Any(name => name.StartsWith("BY", StringComparison.Ordinal) && name != "BYSETPOS"))
        {
            throw Invalid(parameterName, "BYSETPOS requires at least one other BY rule.");
        }

        ValidateFrequencyCombinations(clauses, frequency, parameterName);
    }

    private static void ValidateFrequencyCombinations(
        IReadOnlyDictionary<string, string> clauses,
        string frequency,
        string parameterName)
    {
        if (frequency == "WEEKLY" && clauses.ContainsKey("BYMONTHDAY"))
        {
            throw Invalid(parameterName, "BYMONTHDAY cannot be combined with a weekly recurrence.");
        }

        if (frequency != "YEARLY" && clauses.ContainsKey("BYWEEKNO"))
        {
            throw Invalid(parameterName, "BYWEEKNO requires a yearly recurrence.");
        }

        if (frequency is "DAILY" or "WEEKLY" or "MONTHLY" && clauses.ContainsKey("BYYEARDAY"))
        {
            throw Invalid(parameterName, "BYYEARDAY cannot be combined with a daily, weekly, or monthly recurrence.");
        }

        if (clauses.TryGetValue("BYDAY", out var byDay)
            && byDay.Split(',').Any(HasWeekdayOrdinal)
            && (frequency is not ("MONTHLY" or "YEARLY")
                || frequency == "YEARLY" && clauses.ContainsKey("BYWEEKNO")))
        {
            throw Invalid(parameterName, "Numeric BYDAY values require a monthly or yearly recurrence and cannot be combined with BYWEEKNO.");
        }
    }

    private static bool HasWeekdayOrdinal(string value) => value.Length > 2;

    private static void ValidateClause(
        string name,
        string value,
        bool isAllDay,
        string parameterName)
    {
        if (isAllDay && name is "BYSECOND" or "BYMINUTE" or "BYHOUR")
        {
            throw Invalid(parameterName, name + " cannot be used with an all-day recurrence.");
        }

        switch (name)
        {
            case "FREQ":
                return;
            case "UNTIL":
                ValidateUntil(value, isAllDay, parameterName);
                return;
            case "COUNT":
            case "INTERVAL":
                ValidateInteger(value, 1, int.MaxValue, allowZero: false, parameterName, name);
                return;
            case "BYSECOND":
                ValidateIntegerList(value, 0, 59, allowZero: true, parameterName, name);
                return;
            case "BYMINUTE":
                ValidateIntegerList(value, 0, 59, allowZero: true, parameterName, name);
                return;
            case "BYHOUR":
                ValidateIntegerList(value, 0, 23, allowZero: true, parameterName, name);
                return;
            case "BYMONTHDAY":
                ValidateIntegerList(value, -31, 31, allowZero: false, parameterName, name);
                return;
            case "BYYEARDAY":
                ValidateIntegerList(value, -366, 366, allowZero: false, parameterName, name);
                return;
            case "BYWEEKNO":
                ValidateIntegerList(value, -53, 53, allowZero: false, parameterName, name);
                return;
            case "BYMONTH":
                ValidateIntegerList(value, 1, 12, allowZero: false, parameterName, name);
                return;
            case "BYSETPOS":
                ValidateIntegerList(value, -366, 366, allowZero: false, parameterName, name);
                return;
            case "BYEASTER":
                ValidateIntegerList(value, int.MinValue, int.MaxValue, allowZero: true, parameterName, name);
                return;
            case "BYDAY":
                ValidateWeekdays(value, parameterName);
                return;
            case "WKST":
                if (!Weekdays.Contains(value))
                {
                    throw Invalid(parameterName, "WKST must be a two-letter weekday.");
                }
                return;
            default:
                throw Invalid(parameterName, $"Unsupported recurrence clause: {name}.");
        }
    }

    private static void ValidateUntil(string value, bool isAllDay, string parameterName)
    {
        var format = isAllDay ? "yyyyMMdd" : "yyyyMMdd'T'HHmmss'Z'";
        if (!DateTime.TryParseExact(
                value,
                format,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _))
        {
            throw Invalid(
                parameterName,
                isAllDay
                    ? "UNTIL for an all-day event must be an iCalendar date."
                    : "UNTIL for a timed event must be an iCalendar UTC date-time ending in Z.");
        }
    }

    private static void ValidateIntegerList(
        string value,
        int minimum,
        int maximum,
        bool allowZero,
        string parameterName,
        string clauseName)
    {
        var values = value.Split(',');
        if (values.Length == 0)
        {
            throw Invalid(parameterName, $"{clauseName} requires at least one value.");
        }

        foreach (var item in values)
        {
            ValidateInteger(item, minimum, maximum, allowZero, parameterName, clauseName);
        }
    }

    private static void ValidateInteger(
        string value,
        int minimum,
        int maximum,
        bool allowZero,
        string parameterName,
        string clauseName)
    {
        if (!int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            || number < minimum
            || number > maximum
            || (!allowZero && number == 0))
        {
            throw Invalid(parameterName, $"{clauseName} contains an invalid integer value.");
        }
    }

    private static void ValidateWeekdays(string value, string parameterName)
    {
        foreach (var item in value.Split(','))
        {
            if (item.Length < 2)
            {
                throw Invalid(parameterName, "BYDAY contains an invalid weekday.");
            }

            var weekday = item.Substring(item.Length - 2);
            if (!Weekdays.Contains(weekday))
            {
                throw Invalid(parameterName, "BYDAY contains an invalid weekday.");
            }

            var ordinal = item.Substring(0, item.Length - 2);
            if (ordinal.Length > 0)
            {
                ValidateInteger(ordinal, -53, 53, allowZero: false, parameterName, "BYDAY");
            }
        }
    }

    private static ArgumentException Invalid(string parameterName, string message)
    {
        return new ArgumentException(message, parameterName);
    }
}
