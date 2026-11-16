namespace WorkLocationTracker.Core.Models;

public sealed record PublicHoliday(
    DateOnly Date,
    string Name,
    HolidayType Type = HolidayType.PublicHoliday,
    int LeaveMinutes = 0);
