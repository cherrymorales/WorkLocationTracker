using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Tests;

public sealed class PeriodProgressCalculatorTests
{
    [Fact]
    public void Calculate_ExcludesHolidaysFromTargetDaysAndListsFutureEvents()
    {
        var from = new DateOnly(2026, 9, 7);
        var to = new DateOnly(2026, 9, 13);
        var through = new DateOnly(2026, 9, 9);
        var holidays = new[]
        {
            new PublicHoliday(new DateOnly(2026, 9, 8), "Personal leave"),
            new PublicHoliday(new DateOnly(2026, 9, 11), "Show holiday")
        };
        var sessions = new[]
        {
            Session(new DateOnly(2026, 9, 7), 7.6),
            Session(new DateOnly(2026, 9, 8), 2),
            Session(new DateOnly(2026, 9, 9), 7.5)
        };

        var result = PeriodProgressCalculator.Calculate(
            from,
            to,
            through,
            WorkScheduleSettings.Default,
            holidays,
            new[] { Day(new DateOnly(2026, 9, 7), homeHours: 7.6) },
            sessions);

        Assert.Equal(1, result.CompletedTargetDays);
        Assert.Equal(3, result.ScheduledWorkDays);
        Assert.Equal(1, result.HomeCompletedTargetDays);
        Assert.Equal(0, result.OfficeCompletedTargetDays);
        Assert.Equal(17.1, result.TotalWorkHours, precision: 3);
        Assert.Equal(120, result.AdditionalMinutes);
        Assert.Equal(holidays, result.Events);
    }

    [Fact]
    public void Calculate_TreatsWeekendWorkAsAdditionalTime()
    {
        var saturday = new DateOnly(2026, 9, 12);

        var result = PeriodProgressCalculator.Calculate(
            saturday,
            saturday.AddDays(1),
            saturday.AddDays(1),
            WorkScheduleSettings.Default,
            Array.Empty<PublicHoliday>(),
            Array.Empty<DailyUsageReport>(),
            new[] { Session(saturday, 3.25) });

        Assert.Equal(0, result.CompletedTargetDays);
        Assert.Equal(0, result.ScheduledWorkDays);
        Assert.Equal(3.25, result.TotalWorkHours);
        Assert.Equal(195, result.AdditionalMinutes);
    }

    [Fact]
    public void Calculate_UsesFullMonthForScheduledDaysButHoursOnlyThroughSelectedDate()
    {
        var from = new DateOnly(2026, 10, 1);
        var to = new DateOnly(2026, 10, 31);
        var through = new DateOnly(2026, 10, 2);
        var sessions = new[]
        {
            Session(new DateOnly(2026, 10, 1), 8),
            Session(new DateOnly(2026, 10, 5), 8)
        };

        var result = PeriodProgressCalculator.Calculate(
            from,
            to,
            through,
            WorkScheduleSettings.Default,
            Array.Empty<PublicHoliday>(),
            new[] { Day(new DateOnly(2026, 10, 1), homeHours: 8) },
            sessions);

        Assert.Equal(1, result.CompletedTargetDays);
        Assert.Equal(22, result.ScheduledWorkDays);
        Assert.Equal(8, result.TotalWorkHours);
        Assert.Equal(24, result.AdditionalMinutes);
    }

    [Fact]
    public void Calculate_AssignsCompletedDaysToTheirPredominantLocation()
    {
        var monday = new DateOnly(2026, 9, 7);
        var tuesday = monday.AddDays(1);
        var wednesday = monday.AddDays(2);
        var days = new[]
        {
            Day(monday, homeHours: 2, officeHours: 6),
            Day(tuesday, homeHours: 5, officeHours: 3),
            Day(wednesday, homeHours: 4, officeHours: 4)
        };
        var sessions = new[]
        {
            Session(monday, 8, LocationType.Office),
            Session(tuesday, 8),
            Session(wednesday, 8)
        };

        var result = PeriodProgressCalculator.Calculate(
            monday,
            wednesday,
            wednesday,
            WorkScheduleSettings.Default,
            Array.Empty<PublicHoliday>(),
            days,
            sessions);

        Assert.Equal(3, result.CompletedTargetDays);
        Assert.Equal(1, result.HomeCompletedTargetDays);
        Assert.Equal(1, result.OfficeCompletedTargetDays);
    }

    [Fact]
    public void Calculate_IncludesPartialLeaveDaysWithTheirReducedTarget()
    {
        var monday = new DateOnly(2026, 9, 7);
        var holidays = new[]
        {
            new PublicHoliday(monday, "Appointment", HolidayType.AnnualLeave, LeaveMinutes: 180),
            new PublicHoliday(
                monday.AddDays(1),
                string.Empty,
                HolidayType.SickLeave,
                LeaveMinutes: WorkScheduleSettings.Default.DailyTargetMinutes)
        };

        var result = PeriodProgressCalculator.Calculate(
            monday,
            monday.AddDays(4),
            monday.AddDays(4),
            WorkScheduleSettings.Default,
            holidays,
            new[] { Day(monday, homeHours: 5) },
            new[] { Session(monday, 5) });

        Assert.Equal(1, result.CompletedTargetDays);
        Assert.Equal(4, result.ScheduledWorkDays);
        Assert.Equal(24, result.AdditionalMinutes);
        Assert.Equal(1, result.HomeCompletedTargetDays);
    }

    private static WorkSessionReport Session(
        DateOnly date,
        double workHours,
        LocationType location = LocationType.Home) =>
        new(
            date,
            Guid.NewGuid().ToString("N"),
            new DateTimeOffset(date.ToDateTime(new TimeOnly(8, 0)), TimeSpan.FromHours(10)),
            new DateTimeOffset(date.ToDateTime(new TimeOnly(16, 0)), TimeSpan.FromHours(10)),
            0,
            workHours,
            location.ToString(),
            WorkSessionStatus.Confirmed,
            WorkSessionSource.Manual,
            string.Empty);

    private static DailyUsageReport Day(
        DateOnly date,
        double homeHours = 0,
        double officeHours = 0,
        double otherHours = 0,
        double unknownHours = 0) =>
        new(
            date,
            (int)Math.Round(homeHours * 3600),
            (int)Math.Round(officeHours * 3600),
            (int)Math.Round(otherHours * 3600),
            (int)Math.Round(unknownHours * 3600),
            0,
            0,
            0);
}
