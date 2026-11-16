using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Tests;

public sealed class WorkdayCompletionCalculatorTests
{
    [Fact]
    public void Calculate_EstimatesFromTheLatestSessionEndForAPastDate()
    {
        var date = new DateOnly(2026, 9, 7);
        var session = Session(date, 8, 0, 10, 0, 2);

        var result = WorkdayCompletionCalculator.Calculate(
            date,
            456,
            new[] { session },
            new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.FromHours(10)));

        Assert.Equal(TargetCompletionStatus.Estimated, result.Status);
        Assert.Equal(new TimeOnly(15, 36), TimeOnly.FromDateTime(result.Time!.Value.DateTime));
    }

    [Fact]
    public void Calculate_UsesNowWhenTodaysLatestSessionHasAlreadyEnded()
    {
        var date = new DateOnly(2026, 9, 30);
        var session = Session(date, 8, 0, 10, 0, 2);

        var result = WorkdayCompletionCalculator.Calculate(
            date,
            456,
            new[] { session },
            new DateTimeOffset(2026, 9, 30, 13, 0, 0, TimeSpan.FromHours(10)));

        Assert.Equal(TargetCompletionStatus.Estimated, result.Status);
        Assert.Equal(new TimeOnly(18, 36), TimeOnly.FromDateTime(result.Time!.Value.DateTime));
    }

    [Fact]
    public void Calculate_ReportsTheExactTimeWithinTheSessionThatTheTargetWasCompleted()
    {
        var date = new DateOnly(2026, 9, 7);
        var sessions = new[]
        {
            Session(date, 8, 0, 11, 0, 3),
            Session(date, 12, 0, 17, 0, 5)
        };

        var result = WorkdayCompletionCalculator.Calculate(
            date,
            456,
            sessions,
            sessions[^1].EndTime);

        Assert.Equal(TargetCompletionStatus.Completed, result.Status);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 7, 16, 36, 0, TimeSpan.FromHours(10)),
            result.Time);
    }

    [Fact]
    public void Calculate_IncludesTheReviewedBreakBeforeCompletion()
    {
        var date = new DateOnly(2026, 9, 7);
        var session = Session(date, 8, 0, 17, 0, 8.5, breakMinutes: 30);

        var result = WorkdayCompletionCalculator.Calculate(
            date,
            456,
            new[] { session },
            session.EndTime);

        Assert.Equal(TargetCompletionStatus.Completed, result.Status);
        Assert.Equal(
            new DateTimeOffset(2026, 9, 7, 16, 6, 0, TimeSpan.FromHours(10)),
            result.Time);
    }

    [Fact]
    public void Calculate_HasNoCompletionTimeForAZeroTarget()
    {
        var date = new DateOnly(2026, 9, 5);
        var result = WorkdayCompletionCalculator.Calculate(
            date,
            0,
            new[] { Session(date, 8, 0, 10, 0, 2) },
            new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.FromHours(10)));

        Assert.Equal(TargetCompletionStatus.NotApplicable, result.Status);
        Assert.Null(result.Time);
    }

    private static WorkSessionReport Session(
        DateOnly date,
        int startHour,
        int startMinute,
        int endHour,
        int endMinute,
        double workHours,
        int breakMinutes = 0) =>
        new(
            date,
            Guid.NewGuid().ToString("N"),
            new DateTimeOffset(date.ToDateTime(new TimeOnly(startHour, startMinute)), TimeSpan.FromHours(10)),
            new DateTimeOffset(date.ToDateTime(new TimeOnly(endHour, endMinute)), TimeSpan.FromHours(10)),
            breakMinutes,
            workHours,
            "Home",
            WorkSessionStatus.Confirmed,
            WorkSessionSource.Manual,
            string.Empty);
}
