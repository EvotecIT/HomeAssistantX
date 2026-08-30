using System.Globalization;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Calendars;

internal static class HomeAssistantRecurrenceRuleValidator
{
    private static readonly HashSet<string> SupportedFrequencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "DAILY", "WEEKLY", "MONTHLY", "YEARLY"
    };

    private static readonly HashSet<string> Weekdays = new(StringComparer.OrdinalIgnoreCase)
    {
        "MO", "TU", "WE", "TH", "FR", "SA", "SU"
    };

    public static void Validate(string value, bool isAllDay, string parameterName)
        => Validate(value, isAllDay, parameterName, default);

    internal static void Validate(
        string value,
        bool isAllDay,
        string parameterName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken))
        {
            throw Invalid(parameterName, "A recurrence rule cannot be empty.");
        }

        var clauses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var clauseStart = 0;
        while (clauseStart <= value.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var clauseEnd = FindDelimiter(value, clauseStart, ';', cancellationToken);
            if (clauseEnd < 0) clauseEnd = value.Length;
            var separator = FindDelimiter(value, clauseStart, '=', cancellationToken, clauseEnd);
            if (separator <= clauseStart
                || separator == clauseEnd - 1
                || FindDelimiter(value, separator + 1, '=', cancellationToken, clauseEnd) >= 0)
            {
                throw Invalid(parameterName, "Every recurrence clause must use NAME=VALUE syntax.");
            }

            var nameLength = separator - clauseStart;
            if (nameLength > 10)
            {
                throw Invalid(parameterName, "The recurrence rule contains an unsupported clause.");
            }

            var name = CancellationAwareString.Slice(value, clauseStart, nameLength, cancellationToken);
            var clauseValue = CancellationAwareString.Slice(
                value,
                separator + 1,
                clauseEnd - separator - 1,
                cancellationToken);
            if (clauses.ContainsKey(name))
            {
                throw Invalid(parameterName, $"The recurrence rule contains duplicate {name} clauses.");
            }

            clauses.Add(name, clauseValue);
            if (clauseEnd == value.Length) break;
            clauseStart = clauseEnd + 1;
        }

        if (!clauses.TryGetValue("FREQ", out var frequency)
            || !Contains(SupportedFrequencies, frequency, cancellationToken))
        {
            throw Invalid(
                parameterName,
                "The recurrence rule must contain one supported FREQ value: DAILY, WEEKLY, MONTHLY, or YEARLY.");
        }

        foreach (var clause in clauses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateClause(clause.Key, clause.Value, isAllDay, parameterName, cancellationToken);
        }

        if (clauses.ContainsKey("COUNT") && clauses.ContainsKey("UNTIL"))
        {
            throw Invalid(parameterName, "COUNT and UNTIL cannot be combined in one recurrence rule.");
        }

        if (clauses.ContainsKey("BYSETPOS")
            && !HasCompanionByClause(clauses.Keys, cancellationToken))
        {
            throw Invalid(parameterName, "BYSETPOS requires at least one other BY rule.");
        }

        ValidateFrequencyCombinations(clauses, frequency, parameterName, cancellationToken);
    }

    private static void ValidateFrequencyCombinations(
        IReadOnlyDictionary<string, string> clauses,
        string frequency,
        string parameterName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CancellationAwareString.EqualsOrdinalIgnoreCase(frequency, "WEEKLY", cancellationToken)
            && clauses.ContainsKey("BYMONTHDAY"))
        {
            throw Invalid(parameterName, "BYMONTHDAY cannot be combined with a weekly recurrence.");
        }

        if (!CancellationAwareString.EqualsOrdinalIgnoreCase(frequency, "YEARLY", cancellationToken)
            && clauses.ContainsKey("BYWEEKNO"))
        {
            throw Invalid(parameterName, "BYWEEKNO requires a yearly recurrence.");
        }

        if ((CancellationAwareString.EqualsOrdinalIgnoreCase(frequency, "DAILY", cancellationToken)
                || CancellationAwareString.EqualsOrdinalIgnoreCase(frequency, "WEEKLY", cancellationToken)
                || CancellationAwareString.EqualsOrdinalIgnoreCase(frequency, "MONTHLY", cancellationToken))
            && clauses.ContainsKey("BYYEARDAY"))
        {
            throw Invalid(parameterName, "BYYEARDAY cannot be combined with a daily, weekly, or monthly recurrence.");
        }

        if (clauses.TryGetValue("BYDAY", out var byDay)
            && HasWeekdayOrdinal(byDay, cancellationToken)
            && ((!CancellationAwareString.EqualsOrdinalIgnoreCase(frequency, "MONTHLY", cancellationToken)
                    && !CancellationAwareString.EqualsOrdinalIgnoreCase(frequency, "YEARLY", cancellationToken))
                || CancellationAwareString.EqualsOrdinalIgnoreCase(frequency, "YEARLY", cancellationToken)
                    && clauses.ContainsKey("BYWEEKNO")))
        {
            throw Invalid(parameterName, "Numeric BYDAY values require a monthly or yearly recurrence and cannot be combined with BYWEEKNO.");
        }
    }

    private static bool HasWeekdayOrdinal(string value, CancellationToken cancellationToken)
    {
        var itemStart = 0;
        while (itemStart <= value.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemEnd = FindDelimiter(value, itemStart, ',', cancellationToken);
            if (itemEnd < 0) itemEnd = value.Length;
            if (itemEnd - itemStart > 2) return true;
            if (itemEnd == value.Length) return false;
            itemStart = itemEnd + 1;
        }
        return false;
    }

    private static void ValidateClause(
        string name,
        string value,
        bool isAllDay,
        string parameterName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (isAllDay && name is "BYSECOND" or "BYMINUTE" or "BYHOUR")
        {
            throw Invalid(parameterName, name + " cannot be used with an all-day recurrence.");
        }

        switch (name.ToUpperInvariant())
        {
            case "FREQ":
                return;
            case "UNTIL":
                ValidateUntil(value, isAllDay, parameterName, cancellationToken);
                return;
            case "COUNT":
            case "INTERVAL":
                ValidateInteger(value, 1, int.MaxValue, allowZero: false, parameterName, name, cancellationToken);
                return;
            case "BYSECOND":
                ValidateIntegerList(value, 0, 60, allowZero: true, parameterName, name, cancellationToken);
                return;
            case "BYMINUTE":
                ValidateIntegerList(value, 0, 59, allowZero: true, parameterName, name, cancellationToken);
                return;
            case "BYHOUR":
                ValidateIntegerList(value, 0, 23, allowZero: true, parameterName, name, cancellationToken);
                return;
            case "BYMONTHDAY":
                ValidateIntegerList(value, -31, 31, allowZero: false, parameterName, name, cancellationToken);
                return;
            case "BYYEARDAY":
                ValidateIntegerList(value, -366, 366, allowZero: false, parameterName, name, cancellationToken);
                return;
            case "BYWEEKNO":
                ValidateIntegerList(value, -53, 53, allowZero: false, parameterName, name, cancellationToken);
                return;
            case "BYMONTH":
                ValidateIntegerList(value, 1, 12, allowZero: false, parameterName, name, cancellationToken);
                return;
            case "BYSETPOS":
                ValidateIntegerList(value, -366, 366, allowZero: false, parameterName, name, cancellationToken);
                return;
            case "BYEASTER":
                ValidateIntegerList(value, int.MinValue, int.MaxValue, allowZero: true, parameterName, name, cancellationToken);
                return;
            case "BYDAY":
                ValidateWeekdays(value, parameterName, cancellationToken);
                return;
            case "WKST":
                if (!Contains(Weekdays, value, cancellationToken))
                {
                    throw Invalid(parameterName, "WKST must be a two-letter weekday.");
                }
                return;
            default:
                throw Invalid(parameterName, $"Unsupported recurrence clause: {name}.");
        }
    }

    private static void ValidateUntil(
        string value,
        bool isAllDay,
        string parameterName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var format = isAllDay ? "yyyyMMdd" : "yyyyMMdd'T'HHmmss'Z'";
        if (value.Length != (isAllDay ? 8 : 16)
            || !DateTime.TryParseExact(
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
        string clauseName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.Length == 0)
        {
            throw Invalid(parameterName, $"{clauseName} requires at least one value.");
        }

        var itemStart = 0;
        while (itemStart <= value.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemEnd = FindDelimiter(value, itemStart, ',', cancellationToken);
            if (itemEnd < 0) itemEnd = value.Length;
            var item = CancellationAwareString.Slice(value, itemStart, itemEnd - itemStart, cancellationToken);
            ValidateInteger(item, minimum, maximum, allowZero, parameterName, clauseName, cancellationToken);
            if (itemEnd == value.Length) break;
            itemStart = itemEnd + 1;
        }
    }

    private static void ValidateInteger(
        string value,
        int minimum,
        int maximum,
        bool allowZero,
        string parameterName,
        string clauseName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.Length == 0
            || value.Length > 11
            || !int.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
            || number < minimum
            || number > maximum
            || (!allowZero && number == 0))
        {
            throw Invalid(parameterName, $"{clauseName} contains an invalid integer value.");
        }
    }

    private static void ValidateWeekdays(
        string value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        var itemStart = 0;
        while (itemStart <= value.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var itemEnd = FindDelimiter(value, itemStart, ',', cancellationToken);
            if (itemEnd < 0) itemEnd = value.Length;
            var item = CancellationAwareString.Slice(value, itemStart, itemEnd - itemStart, cancellationToken);
            if (item.Length < 2)
            {
                throw Invalid(parameterName, "BYDAY contains an invalid weekday.");
            }

            var weekday = item.Substring(item.Length - 2);
            if (!Contains(Weekdays, weekday, cancellationToken))
            {
                throw Invalid(parameterName, "BYDAY contains an invalid weekday.");
            }

            var ordinal = item.Substring(0, item.Length - 2);
            if (ordinal.Length > 0)
            {
                ValidateInteger(ordinal, -53, 53, allowZero: false, parameterName, "BYDAY", cancellationToken);
            }

            if (itemEnd == value.Length) break;
            itemStart = itemEnd + 1;
        }
    }

    private static int FindDelimiter(
        string value,
        int start,
        char delimiter,
        CancellationToken cancellationToken,
        int? exclusiveEnd = null)
    {
        var end = exclusiveEnd ?? value.Length;
        for (var index = start; index < end; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (value[index] == delimiter) return index;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return -1;
    }

    private static bool Contains(
        IEnumerable<string> values,
        string candidate,
        CancellationToken cancellationToken)
    {
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (CancellationAwareString.EqualsOrdinalIgnoreCase(value, candidate, cancellationToken)) return true;
        }
        return false;
    }

    private static bool HasCompanionByClause(
        IEnumerable<string> names,
        CancellationToken cancellationToken)
    {
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (name.Length >= 2
                && (name[0] == 'B' || name[0] == 'b')
                && (name[1] == 'Y' || name[1] == 'y')
                && !CancellationAwareString.EqualsOrdinalIgnoreCase(name, "BYSETPOS", cancellationToken))
            {
                return true;
            }
        }
        return false;
    }

    private static ArgumentException Invalid(string parameterName, string message)
    {
        return new ArgumentException(message, parameterName);
    }
}
