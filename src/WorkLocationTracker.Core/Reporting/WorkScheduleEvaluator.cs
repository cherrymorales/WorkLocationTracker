using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Core.Reporting;

public enum ScheduleDayKind
{
    WorkingDay,
    NonWorkingDay,
    PublicHoliday,
    LeaveDay
}

public sealed record EffectiveDailyTarget(
    int TargetMinutes,
    ScheduleDayKind DayKind,
    PublicHoliday? PublicHoliday)
{
    public bool IsNonWorkingDay => TargetMinutes == 0;
}

public static class WorkScheduleEvaluator
{
    public static EffectiveDailyTarget Evaluate(
        DateOnly date,
        WorkScheduleSettings settings,
        IEnumerable<PublicHoliday> publicHolidays)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(publicHolidays);

        var holiday = publicHolidays.FirstOrDefault(item => item.Date == date);
        if (holiday?.Type == HolidayType.PublicHoliday)
        {
            return new EffectiveDailyTarget(0, ScheduleDayKind.PublicHoliday, holiday);
        }

        if (!settings.IsWorkingDay(date.DayOfWeek))
        {
            return new EffectiveDailyTarget(0, ScheduleDayKind.NonWorkingDay, holiday);
        }

        if (holiday is not null)
        {
            return new EffectiveDailyTarget(
                Math.Max(0, settings.DailyTargetMinutes - holiday.LeaveMinutes),
                ScheduleDayKind.LeaveDay,
                holiday);
        }

        return new EffectiveDailyTarget(settings.DailyTargetMinutes, ScheduleDayKind.WorkingDay, null);
    }
}
