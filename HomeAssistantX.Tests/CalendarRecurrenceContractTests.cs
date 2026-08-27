using HomeAssistantX.Calendars;

namespace HomeAssistantX.Tests;

public sealed class CalendarRecurrenceContractTests
{
    [Theory]
    [InlineData("FREQ=DAILY")]
    [InlineData("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR;COUNT=10")]
    [InlineData("FREQ=MONTHLY;BYMONTHDAY=-1;UNTIL=20261231T235959Z")]
    [InlineData("FREQ=YEARLY;BYMONTH=3;BYDAY=2SU;WKST=MO")]
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
    [InlineData("freq=weekly")]
    [InlineData("FREQ=WEEKLY;COUNT=abc")]
    [InlineData("FREQ=WEEKLY;FREQ=DAILY")]
    [InlineData("FREQ=WEEKLY;INTERVAL=0")]
    [InlineData("FREQ=WEEKLY;BYDAY=MONDAY")]
    [InlineData("FREQ=WEEKLY;COUNT=5;UNTIL=20261231")]
    [InlineData("FREQ=WEEKLY;")]
    [InlineData("FREQ=WEEKLY;FUTURE=value")]
    public void CalendarInputRejectsMalformedRecurrenceClauses(string rule)
    {
        var input = HomeAssistantCalendarEventInput.AllDay(
            "2026-08-27",
            "2026-08-28",
            "Recurring event");

        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = rule);
    }
}
