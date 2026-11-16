using WorkLocationTracker.Core.Reporting;
using WorkLocationTracker.Monitor.ViewModels;

namespace WorkLocationTracker.Tests;

public sealed class MonthlyUsageRowViewModelTests
{
    [Fact]
    public void Constructor_MapsDailyReportAndCalculatesTotalHours()
    {
        var day = new DailyUsageReport(
            new DateOnly(2026, 9, 4),
            HomeTrackedSeconds: 3600,
            OfficeTrackedSeconds: 1800,
            OtherTrackedSeconds: 900,
            UnknownTrackedSeconds: 1800,
            HomeAdjustmentMinutes: 30,
            OfficeAdjustmentMinutes: 0,
            OtherAdjustmentMinutes: -5,
            StartTime: new DateTimeOffset(2026, 9, 4, 8, 5, 0, TimeSpan.FromHours(10)),
            EndTime: new DateTimeOffset(2026, 9, 4, 17, 10, 0, TimeSpan.FromHours(10)));

        var row = new MonthlyUsageRowViewModel(
            day,
            breakMinutes: 75,
            additionalMinutes: 99);

        Assert.Equal("Fri 4 Sept 2026", row.DateDisplay);
        Assert.Equal("8:05 am", row.StartTimeDisplay, ignoreCase: true);
        Assert.Equal("5:10 pm", row.EndTimeDisplay, ignoreCase: true);
        Assert.Equal("1 h 15 min", row.BreakDurationDisplay);
        Assert.Equal(1.5, row.HomeHours, precision: 5);
        Assert.Equal("1 h 30 min", row.HomeDurationDisplay);
        Assert.Equal(0.5, row.OfficeHours, precision: 5);
        Assert.Equal("0 h 30 min", row.OfficeDurationDisplay);
        Assert.Equal(1d / 6d, row.OtherHours, precision: 5);
        Assert.Equal("0 h 10 min", row.OtherDurationDisplay);
        Assert.Equal(0.5, row.UnknownHours, precision: 5);
        Assert.Equal("0 h 30 min", row.UnknownDurationDisplay);
        Assert.Equal(2d + 2d / 3d, row.TotalHours, precision: 5);
        Assert.Equal("2 h 40 min", row.TotalDurationDisplay);
        Assert.Equal(99, row.AdditionalMinutes);
        Assert.Equal("1 h 39 min", row.AdditionalDurationDisplay);
    }
}
