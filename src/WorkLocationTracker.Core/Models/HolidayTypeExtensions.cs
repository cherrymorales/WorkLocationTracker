namespace WorkLocationTracker.Core.Models;

public static class HolidayTypeExtensions
{
    public static string ToDisplayName(this HolidayType type) => type switch
    {
        HolidayType.PublicHoliday => "Public Holiday",
        HolidayType.AnnualLeave => "Annual Leave",
        HolidayType.SickLeave => "Sick Leave",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Holiday type.")
    };
}
