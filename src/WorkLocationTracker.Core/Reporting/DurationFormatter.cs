namespace WorkLocationTracker.Core.Reporting;

public static class DurationFormatter
{
    public static string FormatHours(double hours)
    {
        if (!double.IsFinite(hours) || hours < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(hours), "Hours must be a finite, non-negative value.");
        }

        var totalMinutes = checked((long)Math.Round(hours * 60, MidpointRounding.AwayFromZero));
        return FormatMinutes(totalMinutes);
    }

    public static string FormatMinutes(int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minutes);
        return FormatMinutes((long)minutes);
    }

    private static string FormatMinutes(long totalMinutes)
    {
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;
        return $"{hours} h {minutes:00} min";
    }
}
