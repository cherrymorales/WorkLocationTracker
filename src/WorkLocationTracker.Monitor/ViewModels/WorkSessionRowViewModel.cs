using System.Globalization;
using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;

namespace WorkLocationTracker.Monitor.ViewModels;

public sealed class WorkSessionRowViewModel
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-AU");

    public WorkSessionRowViewModel(WorkSessionRecord record, WorkSessionReport report)
    {
        Record = record;
        StartTimeDisplay = report.StartTime.ToString("h:mm tt", DisplayCulture);
        EndTimeDisplay = report.EndTime.ToString("h:mm tt", DisplayCulture);
        BreakMinutes = report.BreakMinutes;
        WorkHours = report.WorkHours;
        Location = report.Location;
        Status = report.Status == WorkSessionStatus.Confirmed ? "Confirmed" : "Needs review";
        Source = report.Source switch
        {
            WorkSessionSource.ImportedSummary => "Imported summary",
            WorkSessionSource.Manual => "Manual",
            _ => "Automatic"
        };
        Note = report.Note;
    }

    public WorkSessionRecord Record { get; }

    public string StartTimeDisplay { get; }

    public string EndTimeDisplay { get; }

    public int BreakMinutes { get; }

    public string BreakDurationDisplay => DurationFormatter.FormatMinutes(BreakMinutes);

    public double WorkHours { get; }

    public string WorkDurationDisplay => DurationFormatter.FormatHours(WorkHours);

    public string Location { get; }

    public string Status { get; }

    public string Source { get; }

    public string Note { get; }
}
