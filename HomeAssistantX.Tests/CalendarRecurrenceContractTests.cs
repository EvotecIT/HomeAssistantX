using HomeAssistantX.Calendars;

namespace HomeAssistantX.Tests;

public sealed class CalendarRecurrenceContractTests
{
    [Theory]
    [InlineData("FREQ=DAILY")]
    [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR;COUNT=10")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=-1;UNTIL=20261231")]
    [InlineData("FREQ=YEARLY;BYMONTH=3;BYDAY=2SU;WKST=MO")]
    [InlineData("FREQ=MONTHLY;BYDAY=MO,TU,WE,TH,FR;BYSETPOS=1")]
    [InlineData("freq=weekly;interval=2;byday=mo,we,fr;count=10")]
    public void CalendarInputAcceptsSupportedWellFormedRecurrenceRules(string rule)
    {
        var input = HomeAssistantCalendarEventInput.AllDay(
            "2026-08-27",
            "2026-08-28",
            "Recurring event");

        input.RecurrenceRule = rule;

        Assert.Equal(rule, input.RecurrenceRule);
    }

    [Theory]
    [InlineData("FREQ=WEEKLY;COUNT=abc")]
    [InlineData("FREQ=WEEKLY;FREQ=DAILY")]
    [InlineData("FREQ=WEEKLY;INTERVAL=0")]
    [InlineData("FREQ=DAILY;BYSECOND=60")]
    [InlineData("FREQ=WEEKLY;BYDAY=MONDAY")]
    [InlineData("FREQ=WEEKLY;COUNT=5;UNTIL=20261231")]
    [InlineData("FREQ=WEEKLY;")]
    [InlineData("FREQ=WEEKLY;FUTURE=value")]
    [InlineData("FREQ=DAILY;BYSETPOS=1")]
    [InlineData("FREQ=WEEKLY;BYMONTHDAY=1")]
    [InlineData("FREQ=DAILY;BYYEARDAY=1")]
    [InlineData("FREQ=WEEKLY;BYYEARDAY=1")]
    [InlineData("FREQ=MONTHLY;BYYEARDAY=1")]
    [InlineData("FREQ=MONTHLY;BYWEEKNO=1")]
    [InlineData("FREQ=DAILY;BYDAY=1MO")]
    [InlineData("FREQ=YEARLY;BYWEEKNO=1;BYDAY=1MO")]
    public void CalendarInputRejectsMalformedRecurrenceClauses(string rule)
    {
        var input = HomeAssistantCalendarEventInput.AllDay(
            "2026-08-27",
            "2026-08-28",
            "Recurring event");

        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = rule);
    }

    [Theory]
    [InlineData("FREQ=YEARLY;BYYEARDAY=1")]
    [InlineData("FREQ=YEARLY;BYWEEKNO=1;BYDAY=MO")]
    [InlineData("FREQ=MONTHLY;BYDAY=1MO")]
    public void CalendarInputAcceptsValidFrequencyDependentRules(string rule)
    {
        var input = HomeAssistantCalendarEventInput.AllDay(
            "2026-08-27",
            "2026-08-28",
            "Recurring event");

        input.RecurrenceRule = rule;

        Assert.Equal(rule, input.RecurrenceRule);
    }

    [Fact]
    public void TimedCalendarInputAcceptsLeapSecondRecurrenceValues()
    {
        var input = HomeAssistantCalendarEventInput.Timed(
            new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 27, 11, 0, 0, TimeSpan.Zero),
            "Leap second");

        input.RecurrenceRule = "FREQ=DAILY;BYSECOND=60";

        Assert.Equal("FREQ=DAILY;BYSECOND=60", input.RecurrenceRule);
    }

    [Fact]
    public void CalendarUntilMustMatchTheEventStartValueType()
    {
        var timed = HomeAssistantCalendarEventInput.Timed(
            new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.FromHours(2)),
            new DateTimeOffset(2026, 8, 27, 11, 0, 0, TimeSpan.FromHours(2)),
            "Timed event");
        timed.RecurrenceRule = "FREQ=WEEKLY;UNTIL=20261231T235959Z";
        Assert.Equal("FREQ=WEEKLY;UNTIL=20261231T235959Z", timed.RecurrenceRule);
        Assert.Throws<ArgumentException>(() =>
            timed.RecurrenceRule = "FREQ=WEEKLY;UNTIL=20261231T235959");
        Assert.Throws<ArgumentException>(() =>
            timed.RecurrenceRule = "FREQ=WEEKLY;UNTIL=20261231");

        var allDay = HomeAssistantCalendarEventInput.AllDay(
            "2026-08-27",
            "2026-08-28",
            "All-day event");
        allDay.RecurrenceRule = "FREQ=WEEKLY;UNTIL=20261231";
        Assert.Equal("FREQ=WEEKLY;UNTIL=20261231", allDay.RecurrenceRule);
        Assert.Throws<ArgumentException>(() =>
            allDay.RecurrenceRule = "FREQ=WEEKLY;UNTIL=20261231T235959Z");
    }
}
