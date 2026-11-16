using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Core.Reporting;

public sealed record PeriodProgressSummary(
    int CompletedTargetDays,
    int ScheduledWorkDays,
    int HomeCompletedTargetDays,
    int OfficeCompletedTargetDays,
    double TotalWorkHours,
    int AdditionalMinutes,
    IReadOnlyList<PublicHoliday> Events);

public static class PeriodProgressCalculator
{
    public static PeriodProgressSummary Calculate(
        DateOnly from,
        DateOnly to,
        DateOnly throughDate,
        WorkScheduleSettings settings,
        IReadOnlyCollection<PublicHoliday> holidays,
        IReadOnlyCollection<DailyUsageReport> days,
        IReadOnlyCollection<WorkSessionReport> sessions)
    {
        if (to < from)
        {
            throw new ArgumentException("The period end must not be earlier than its start.", nameof(to));
        }

        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(holidays);
        ArgumentNullException.ThrowIfNull(days);
        ArgumentNullException.ThrowIfNull(sessions);

        var events = holidays
            .Where(holiday => holiday.Date >= from && holiday.Date <= to)
            .OrderBy(holiday => holiday.Date)
            .ToArray();
        var scheduledDates = Enumerable
            .Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(from.AddDays)
            .Where(date => WorkScheduleEvaluator.Evaluate(date, settings, events).TargetMinutes > 0)
            .ToArray();
        var includedSessions = sessions
            .Where(session => session.Date >= from && session.Date <= to && session.Date <= throughDate)
            .ToArray();
        var workHoursByDate = includedSessions
            .GroupBy(session => session.Date)
            .ToDictionary(group => group.Key, group => group.Sum(session => session.WorkHours));

        var completedDates = scheduledDates
            .Where(date => date <= throughDate)
            .Where(date =>
            {
                var target = WorkScheduleEvaluator.Evaluate(date, settings, events).TargetMinutes;
                return WorkdayTargetCalculator
                    .Calculate(workHoursByDate.GetValueOrDefault(date), target)
                    .IsMet;
            })
            .ToArray();
        var dayByDate = days.ToDictionary(day => day.Date);
        var homeCompletedTargetDays = completedDates.Count(date =>
            dayByDate.TryGetValue(date, out var day) && IsPredominantlyHome(day));
        var officeCompletedTargetDays = completedDates.Count(date =>
            dayByDate.TryGetValue(date, out var day) && IsPredominantlyOffice(day));
        var additionalMinutes = workHoursByDate.Sum(pair =>
        {
            var effectiveTarget = WorkScheduleEvaluator.Evaluate(pair.Key, settings, events);
            return WorkdayTargetCalculator.Calculate(pair.Value, effectiveTarget.TargetMinutes).AdditionalMinutes;
        });

        return new PeriodProgressSummary(
            completedDates.Length,
            scheduledDates.Length,
            homeCompletedTargetDays,
            officeCompletedTargetDays,
            includedSessions.Sum(session => session.WorkHours),
            additionalMinutes,
            events);
    }

    private static bool IsPredominantlyHome(DailyUsageReport day) =>
        day.HomeHours > 0 &&
        day.HomeHours > day.OfficeHours &&
        day.HomeHours > day.OtherHours &&
        day.HomeHours > day.UnknownHours;

    private static bool IsPredominantlyOffice(DailyUsageReport day) =>
        day.OfficeHours > 0 &&
        day.OfficeHours > day.HomeHours &&
        day.OfficeHours > day.OtherHours &&
        day.OfficeHours > day.UnknownHours;
}
