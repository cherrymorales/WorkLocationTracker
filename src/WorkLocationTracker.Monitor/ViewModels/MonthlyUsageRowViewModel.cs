using System.Globalization;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Monitor.ViewModels;

public sealed class MonthlyUsageRowViewModel
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-AU");

    public MonthlyUsageRowViewModel(
        DailyUsageReport day,
        int breakMinutes,
        int additionalMinutes)
    {
        ArgumentNullException.ThrowIfNull(day);
        ArgumentOutOfRangeException.ThrowIfNegative(breakMinutes);
        ArgumentOutOfRangeException.ThrowIfNegative(additionalMinutes);

        Date = day.Date;
        HomeHours = day.HomeHours;
        OfficeHours = day.OfficeHours;
        OtherHours = day.OtherHours;
        UnknownHours = day.UnknownHours;
        StartTime = day.StartTime;
        EndTime = day.EndTime;
        BreakMinutes = breakMinutes;
        AdditionalMinutes = additionalMinutes;
    }

    public DateOnly Date { get; }

    public string DateDisplay => Date.ToString("ddd d MMM yyyy", DisplayCulture);

    public DateTimeOffset? StartTime { get; }

    public string StartTimeDisplay => StartTime?.ToString("h:mm tt", DisplayCulture) ?? "—";

    public DateTimeOffset? EndTime { get; }

    public string EndTimeDisplay => EndTime?.ToString("h:mm tt", DisplayCulture) ?? "—";

    public int BreakMinutes { get; }

    public string BreakDurationDisplay => DurationFormatter.FormatMinutes(BreakMinutes);

    public double HomeHours { get; }

    public string HomeDurationDisplay => DurationFormatter.FormatHours(HomeHours);

    public double OfficeHours { get; }

    public string OfficeDurationDisplay => DurationFormatter.FormatHours(OfficeHours);

    public double OtherHours { get; }

    public string OtherDurationDisplay => DurationFormatter.FormatHours(OtherHours);

    public double UnknownHours { get; }

    public string UnknownDurationDisplay => DurationFormatter.FormatHours(UnknownHours);

    public double TotalHours => HomeHours + OfficeHours + OtherHours + UnknownHours;

    public string TotalDurationDisplay => DurationFormatter.FormatHours(TotalHours);

    public int AdditionalMinutes { get; }

    public string AdditionalDurationDisplay => DurationFormatter.FormatMinutes(AdditionalMinutes);
}
