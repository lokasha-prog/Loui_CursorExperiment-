using FluentAssertions;
using NMS.Application.Calendar;
using Xunit;

namespace NMS.UnitTests.Application;

public class RecurrenceTests
{
    private static RepeatRule Rule(string text)
    {
        RepeatRule.TryParse(text, out var rule).Should().BeTrue(text);
        return rule!;
    }

    [Theory]
    [InlineData("FREQ=DAILY")]
    [InlineData("FREQ=WEEKLY;BYDAY=TU,TH;UNTIL=20261231")]
    [InlineData("FREQ=MONTHLY;INTERVAL=2;COUNT=6")]
    [InlineData("")]
    [InlineData(null)]
    public void Supported_rules_are_accepted(string? text) => RepeatRule.TryParse(text, out _).Should().BeTrue();

    [Theory]
    [InlineData("FREQ=YEARLY")]
    [InlineData("FREQ=WEEKLY;BYDAY=XX")]
    [InlineData("FREQ=DAILY;INTERVAL=0")]
    [InlineData("FREQ=DAILY;COUNT=999999")]
    [InlineData("BYDAY=MO")]
    [InlineData("FREQ=DAILY;BYSETPOS=1")]
    [InlineData("rubbish")]
    public void Unsupported_or_unsafe_rules_are_refused(string text) => RepeatRule.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void Weekly_on_tuesday_and_thursday_lands_on_those_days_only()
    {
        var dates = Rule("FREQ=WEEKLY;BYDAY=TU,TH").Dates(new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 5)).ToList();

        dates.Should().Equal(new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 22), new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 29), new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 5));
    }

    [Fact]
    public void Every_second_week_skips_the_weeks_in_between()
    {
        var dates = Rule("FREQ=WEEKLY;INTERVAL=2").Dates(new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 20)).ToList();

        dates.Should().Equal(new DateOnly(2026, 10, 20), new DateOnly(2026, 11, 3), new DateOnly(2026, 11, 17));
    }

    [Fact]
    public void Count_and_until_both_stop_the_repeat()
    {
        Rule("FREQ=DAILY;COUNT=3").Dates(new DateOnly(2026, 1, 1), new DateOnly(2030, 1, 1)).Should().HaveCount(3);
        Rule("FREQ=DAILY;UNTIL=20260105").Dates(new DateOnly(2026, 1, 1), new DateOnly(2030, 1, 1)).Should().HaveCount(5);
    }

    [Fact]
    public void Monthly_skips_months_without_that_day_and_never_runs_past_the_cap()
    {
        var dates = Rule("FREQ=MONTHLY").Dates(new DateOnly(2026, 1, 31), new DateOnly(2026, 6, 30)).ToList();
        var endless = Rule("FREQ=DAILY").Dates(new DateOnly(2026, 1, 1), new DateOnly(2999, 1, 1)).Count();

        dates.Should().Equal(new DateOnly(2026, 1, 31), new DateOnly(2026, 3, 31), new DateOnly(2026, 5, 31));
        endless.Should().Be(RepeatRule.MaxOccurrences);
    }
}
