using HomeAssistantX.Exceptions;
using NodaTime;
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
    internal static HomeAssistantCalendarZone RequireTimeZone(
        string? timeZoneId,
        string purpose,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (CancellationAwareString.IsNullOrWhiteSpace(timeZoneId, cancellationToken))
            throw new HomeAssistantProtocolException($"Home Assistant did not provide the time zone required to validate {purpose}.");
        if (timeZoneId!.Length > 255)
            throw new HomeAssistantProtocolException($"Home Assistant provided an unsupported time zone for {purpose}.");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var timeZone = TZConvert.GetTimeZoneInfo(timeZoneId);
            var ianaZone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId);
            cancellationToken.ThrowIfCancellationRequested();
            return new HomeAssistantCalendarZone(timeZone, ianaZone);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new HomeAssistantProtocolException($"Home Assistant provided an unsupported time zone for {purpose}.");
        }
    }

    internal static bool IsBoundary(
        DateTimeOffset value,
        HomeAssistantCalendarZone homeTimeZone,
        HomeAssistantCalendarPeriod period)
        => value.Ticks % TimeSpan.TicksPerMillisecond == 0
            && IsBoundary(value.ToUnixTimeMilliseconds(), homeTimeZone, period);

    internal static bool IsBoundary(
        long unixMilliseconds,
        HomeAssistantCalendarZone homeTimeZone,
        HomeAssistantCalendarPeriod period)
    {
        var instant = DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        var local = ToLocalDateTime(instant, homeTimeZone);
        if (period == HomeAssistantCalendarPeriod.Week && local.DayOfWeek != DayOfWeek.Monday
            || period == HomeAssistantCalendarPeriod.Month && local.Day != 1)
        {
            return false;
        }

        var localMidnight = new DateTime(
            local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return ResolveBoundary(localMidnight, homeTimeZone).ToUnixTimeMilliseconds() == unixMilliseconds;
    }

    internal static DateTimeOffset ResolveBoundary(DateTime localBoundary, HomeAssistantCalendarZone homeTimeZone)
    {
        if (homeTimeZone.IanaZone is not null)
        {
            var local = LocalDateTime.FromDateTime(DateTime.SpecifyKind(localBoundary, DateTimeKind.Unspecified));
            return homeTimeZone.IanaZone.AtLeniently(local).ToDateTimeOffset();
        }

        TimeSpan offset;
        if (homeTimeZone.SystemZone.IsInvalidTime(localBoundary))
        {
            // Home Assistant uses Python zoneinfo fold=0 semantics. For a local time in a
            // forward gap, that means the offset immediately before the transition and
            // therefore normalizes the requested midnight to the first valid local time.
            offset = homeTimeZone.SystemZone.GetUtcOffset(localBoundary.AddTicks(-1));
        }
        else
        {
            offset = homeTimeZone.SystemZone.IsAmbiguousTime(localBoundary)
                ? homeTimeZone.SystemZone.GetAmbiguousTimeOffsets(localBoundary).Max()
                : homeTimeZone.SystemZone.GetUtcOffset(localBoundary);
        }
        return new DateTimeOffset(localBoundary, offset);
    }

    internal static DateTimeOffset GetContainingBoundary(
        DateTimeOffset value,
        HomeAssistantCalendarZone homeTimeZone,
        HomeAssistantCalendarPeriod period)
    {
        var local = ToLocalDateTime(value, homeTimeZone);
        var localBoundary = new DateTime(
            local.Year,
            local.Month,
            period == HomeAssistantCalendarPeriod.Month ? 1 : local.Day,
            0,
            0,
            0,
            DateTimeKind.Unspecified);
        if (period == HomeAssistantCalendarPeriod.Week)
        {
            var daysSinceMonday = ((int)local.DayOfWeek + 6) % 7;
            localBoundary = localBoundary.AddDays(-daysSinceMonday);
        }

        return ResolveBoundary(localBoundary, homeTimeZone);
    }

    internal static DateTime ToLocalDateTime(DateTimeOffset value, HomeAssistantCalendarZone homeTimeZone)
    {
        if (homeTimeZone.IanaZone is not null)
            return Instant.FromDateTimeOffset(value).InZone(homeTimeZone.IanaZone).LocalDateTime.ToDateTimeUnspecified();
        return TimeZoneInfo.ConvertTime(value, homeTimeZone.SystemZone).DateTime;
    }
}

internal sealed class HomeAssistantCalendarZone
{
    internal HomeAssistantCalendarZone(TimeZoneInfo systemZone, DateTimeZone? ianaZone)
    {
        SystemZone = systemZone;
        IanaZone = ianaZone;
    }

    internal TimeZoneInfo SystemZone { get; }

    internal DateTimeZone? IanaZone { get; }
}
