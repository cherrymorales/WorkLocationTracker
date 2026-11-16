namespace WorkLocationTracker.Core.Models;

public sealed record WorkScheduleSettings(
    int DailyTargetMinutes,
    WorkingDays ScheduledDays)
{
    public static WorkScheduleSettings Default { get; } = new(
        7 * 60 + 36,
        WorkingDays.Weekdays);

    public bool IsWorkingDay(DayOfWeek dayOfWeek)
    {
        var day = dayOfWeek switch
        {
            DayOfWeek.Sunday => WorkingDays.Sunday,
            DayOfWeek.Monday => WorkingDays.Monday,
            DayOfWeek.Tuesday => WorkingDays.Tuesday,
            DayOfWeek.Wednesday => WorkingDays.Wednesday,
            DayOfWeek.Thursday => WorkingDays.Thursday,
            DayOfWeek.Friday => WorkingDays.Friday,
            DayOfWeek.Saturday => WorkingDays.Saturday,
            _ => WorkingDays.None
        };

        return (ScheduledDays & day) != 0;
    }
}
