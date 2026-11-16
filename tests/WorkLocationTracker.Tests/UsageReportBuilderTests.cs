using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Tests;

public sealed class UsageReportBuilderTests
{
    [Fact]
    public void Build_ClassifiesSamplesAndAppliesAuditableAdjustments()
    {
        var date = new DateOnly(2026, 8, 17);
        var start = new DateTimeOffset(2026, 8, 17, 8, 0, 0, TimeSpan.Zero);
        var networks = new[]
        {
            Network("home", LocationType.Home),
            Network("office", LocationType.Office)
        };
        var dailyUsage = new[]
        {
            new DailyUsageRecord(
                date,
                start,
                start.AddHours(2).AddMinutes(15),
                new Dictionary<string, int>
                {
                    ["home"] = 3600,
                    ["office"] = 1800,
                    ["not-classified"] = 900
                },
                0,
                "TEST-MACHINE")
        };
        var adjustments = new[]
        {
            new ManualAdjustment(1, date, LocationType.Home, 30, "Client meeting", start)
        };

        var report = new UsageReportBuilder(TimeZoneInfo.Utc).Build(
            date,
            date,
            0.70m,
            dailyUsage,
            networks,
            adjustments);

        Assert.Equal(1.5, report.TotalHomeHours, precision: 5);
        Assert.Equal(0.5, report.TotalOfficeHours, precision: 5);
        Assert.Equal(0.25, report.TotalUnknownHours, precision: 5);
        Assert.Equal(1.05m, report.EstimatedHomeValue);
        var day = Assert.Single(report.Days);
        Assert.Equal(30, day.HomeAdjustmentMinutes);
        Assert.Equal(start, day.StartTime);
        Assert.Equal(start.AddHours(2).AddMinutes(15), day.EndTime);
    }

    [Fact]
    public void Build_NegativeAdjustmentCannotProduceNegativeDailyHours()
    {
        var date = new DateOnly(2026, 8, 17);
        var adjustment = new ManualAdjustment(
            1,
            date,
            LocationType.Home,
            -120,
            "Remove non-work time",
            DateTimeOffset.UtcNow);

        var report = new UsageReportBuilder(TimeZoneInfo.Utc).Build(
            date,
            date,
            0.70m,
            Array.Empty<DailyUsageRecord>(),
            Array.Empty<NetworkLocation>(),
            new[] { adjustment });

        Assert.Equal(0, report.TotalHomeHours);
    }

    [Fact]
    public void Build_UsesConfirmedSessionTimesAndBreakForDailyTotals()
    {
        var date = new DateOnly(2026, 8, 17);
        var start = new DateTimeOffset(2026, 8, 17, 7, 0, 0, TimeSpan.Zero);
        var session = new WorkSessionRecord(
            "confirmed-session",
            start,
            start.AddHours(10),
            30,
            new Dictionary<string, int> { ["home"] = 3600 },
            LocationType.Home,
            WorkSessionStatus.Confirmed,
            WorkSessionSource.Automatic,
            "Reviewed against timesheet");
        var dailyUsage = new DailyUsageRecord(
            date,
            start,
            start.AddHours(10),
            new Dictionary<string, int> { ["home"] = 3600 },
            0,
            "TEST-MACHINE",
            new[] { session });

        var report = new UsageReportBuilder(TimeZoneInfo.Utc).Build(
            date,
            date,
            0.70m,
            new[] { dailyUsage },
            new[] { Network("home", LocationType.Home) },
            Array.Empty<ManualAdjustment>());

        Assert.Equal(9.5, report.TotalHomeHours, precision: 5);
        var detail = Assert.Single(report.DetailedSessions);
        Assert.Equal(30, detail.BreakMinutes);
        Assert.Equal(9.5, detail.WorkHours, precision: 5);
        Assert.Equal("Home", detail.Location);
    }

    private static NetworkLocation Network(string id, LocationType locationType) =>
        new(id, id, locationType, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

}
