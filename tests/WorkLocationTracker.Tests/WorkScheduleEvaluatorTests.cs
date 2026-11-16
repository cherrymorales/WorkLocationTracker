using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Tests;

public sealed class WorkScheduleEvaluatorTests
{
    [Fact]
    public void Evaluate_UsesConfiguredTargetOnAStandardWorkday()
    {
        var result = WorkScheduleEvaluator.Evaluate(
            new DateOnly(2026, 9, 7),
            WorkScheduleSettings.Default,
            Array.Empty<PublicHoliday>());

        Assert.Equal(456, result.TargetMinutes);
        Assert.Equal(ScheduleDayKind.WorkingDay, result.DayKind);
    }

    [Fact]
    public void Evaluate_UsesZeroTargetOnSaturdayAndSunday()
    {
        var saturday = WorkScheduleEvaluator.Evaluate(
            new DateOnly(2026, 9, 5),
            WorkScheduleSettings.Default,
            Array.Empty<PublicHoliday>());
        var sunday = WorkScheduleEvaluator.Evaluate(
            new DateOnly(2026, 9, 6),
            WorkScheduleSettings.Default,
            Array.Empty<PublicHoliday>());

        Assert.Equal(0, saturday.TargetMinutes);
        Assert.Equal(ScheduleDayKind.NonWorkingDay, saturday.DayKind);
        Assert.Equal(0, sunday.TargetMinutes);
        Assert.Equal(ScheduleDayKind.NonWorkingDay, sunday.DayKind);
    }

    [Fact]
    public void Evaluate_PublicHolidayOverridesAConfiguredWorkday()
    {
        var date = new DateOnly(2026, 10, 5);
        var result = WorkScheduleEvaluator.Evaluate(
            date,
            WorkScheduleSettings.Default,
            new[] { new PublicHoliday(date, "Labour Day") });

        Assert.Equal(0, result.TargetMinutes);
        Assert.Equal(ScheduleDayKind.PublicHoliday, result.DayKind);
        Assert.Equal("Labour Day", result.PublicHoliday?.Name);
    }

    [Fact]
    public void Evaluate_HonoursACustomWeekendWorkday()
    {
        var settings = new WorkScheduleSettings(480, WorkingDays.Saturday);
        var result = WorkScheduleEvaluator.Evaluate(
            new DateOnly(2026, 9, 5),
            settings,
            Array.Empty<PublicHoliday>());

        Assert.Equal(480, result.TargetMinutes);
        Assert.Equal(ScheduleDayKind.WorkingDay, result.DayKind);
    }

    [Fact]
    public void Evaluate_PartialLeaveReducesTheDailyTarget()
    {
        var date = new DateOnly(2026, 9, 7);
        var leave = new PublicHoliday(
            date,
            "Appointment",
            HolidayType.AnnualLeave,
            LeaveMinutes: 180);

        var result = WorkScheduleEvaluator.Evaluate(
            date,
            WorkScheduleSettings.Default,
            new[] { leave });

        Assert.Equal(276, result.TargetMinutes);
        Assert.Equal(ScheduleDayKind.LeaveDay, result.DayKind);
        Assert.Equal(leave, result.PublicHoliday);
    }

    [Fact]
    public void Evaluate_FullDayLeaveUsesAZeroTarget()
    {
        var date = new DateOnly(2026, 9, 7);

        var result = WorkScheduleEvaluator.Evaluate(
            date,
            WorkScheduleSettings.Default,
            new[]
            {
                new PublicHoliday(
                    date,
                    string.Empty,
                    HolidayType.SickLeave,
                    LeaveMinutes: WorkScheduleSettings.Default.DailyTargetMinutes)
            });

        Assert.Equal(0, result.TargetMinutes);
        Assert.Equal(ScheduleDayKind.LeaveDay, result.DayKind);
    }
}
