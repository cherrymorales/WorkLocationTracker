namespace WorkLocationTracker.Core.Reporting;

public sealed record WorkdayTargetProgress(
    int WorkedMinutes,
    int TargetMinutes)
{
    public bool IsMet => WorkedMinutes >= TargetMinutes;

    public int RemainingMinutes => Math.Max(0, TargetMinutes - WorkedMinutes);

    public int AdditionalMinutes => Math.Max(0, WorkedMinutes - TargetMinutes);
}

public static class WorkdayTargetCalculator
{
    public static WorkdayTargetProgress Calculate(double workedHours, int targetMinutes)
    {
        if (!double.IsFinite(workedHours) || workedHours < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(workedHours),
                "Worked hours must be a finite, non-negative value.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(targetMinutes);

        var workedMinutes = checked((int)Math.Round(
            workedHours * 60,
            MidpointRounding.AwayFromZero));
        return new WorkdayTargetProgress(workedMinutes, targetMinutes);
    }

    public static int CalculateAdditionalMinutes(double workedHours, int targetMinutes) =>
        Calculate(workedHours, targetMinutes).AdditionalMinutes;
}
