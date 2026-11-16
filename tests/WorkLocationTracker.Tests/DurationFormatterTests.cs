using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Tests;

public sealed class DurationFormatterTests
{
    [Theory]
    [InlineData(0, "0 h 00 min")]
    [InlineData(0.5, "0 h 30 min")]
    [InlineData(1.5, "1 h 30 min")]
    [InlineData(8.999, "9 h 00 min")]
    public void FormatHours_RoundsToNearestMinute(double hours, string expected)
    {
        Assert.Equal(expected, DurationFormatter.FormatHours(hours));
    }

    [Fact]
    public void FormatMinutes_UsesHourAndMinuteComponents()
    {
        Assert.Equal("2 h 05 min", DurationFormatter.FormatMinutes(125));
    }
}
