using System.Globalization;
using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Monitor.ViewModels;

public sealed class PublicHolidayRowViewModel
{
    public PublicHolidayRowViewModel(PublicHoliday record)
    {
        Record = record;
    }

    public PublicHoliday Record { get; }

    public string DateDisplay => Record.Date.ToString("ddd, d MMM yyyy", CultureInfo.GetCultureInfo("en-AU"));

    public string TypeDisplay => Record.Type.ToDisplayName();

    public string LeaveDurationDisplay => Record.Type == HolidayType.PublicHoliday
        ? "Full day"
        : DurationFormatter.FormatMinutes(Record.LeaveMinutes);

    public string Name => string.IsNullOrWhiteSpace(Record.Name) ? "—" : Record.Name;
}
