using HomeAssistantX.Exceptions;
using TimeZoneConverter;

namespace HomeAssistantX.Protocol;

internal enum HomeAssistantCalendarPeriod
{
    Day,
    Week,
    Month
}

internal static class HomeAssistantCalendarTime
{
    internal static TimeZoneInfo RequireTimeZone(string? timeZoneId, string purpose)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            throw new HomeAssistantProtocolException($"Home Assistant did not provide the time zone required to validate {purpose}.");

        try
        {
            return TZConvert.GetTimeZoneInfo(timeZoneId!);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new HomeAssistantProtocolException($"Home Assistant provided an unsupported time zone for {purpose}.");
        }
    }

    internal static bool IsBoundary(
        DateTimeOffset value,
        TimeZoneInfo homeTimeZone,
        HomeAssistantCalendarPeriod period)
        => IsBoundary(value.ToUnixTimeMilliseconds(), homeTimeZone, period);

    internal static bool IsBoundary(
        long unixMilliseconds,
        TimeZoneInfo homeTimeZone,
        HomeAssistantCalendarPeriod period)
    {
        var local = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds), homeTimeZone);
        if (period == HomeAssistantCalendarPeriod.Week && local.DayOfWeek != DayOfWeek.Monday
            || period == HomeAssistantCalendarPeriod.Month && local.Day != 1)
        {
            return false;
        }

        var localMidnight = new DateTime(
            local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return ResolveBoundary(localMidnight, homeTimeZone).ToUnixTimeMilliseconds() == unixMilliseconds;
    }

    internal static DateTimeOffset ResolveBoundary(DateTime localBoundary, TimeZoneInfo homeTimeZone)
    {
        TimeSpan offset;
        if (homeTimeZone.IsInvalidTime(localBoundary))
        {
            // Home Assistant uses Python zoneinfo fold=0 semantics. For a local time in a
            // forward gap, that means the offset immediately before the transition and
            // therefore normalizes the requested midnight to the first valid local time.
            offset = homeTimeZone.GetUtcOffset(localBoundary.AddTicks(-1));
        }
        else
        {
            offset = homeTimeZone.IsAmbiguousTime(localBoundary)
                ? homeTimeZone.GetAmbiguousTimeOffsets(localBoundary).Max()
                : homeTimeZone.GetUtcOffset(localBoundary);
        }
        return new DateTimeOffset(localBoundary, offset);
    }
}
