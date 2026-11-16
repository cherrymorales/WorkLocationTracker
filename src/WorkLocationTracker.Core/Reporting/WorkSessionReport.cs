using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Core.Reporting;

public sealed record WorkSessionReport(
    DateOnly Date,
    string Id,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    int BreakMinutes,
    double WorkHours,
    string Location,
    WorkSessionStatus Status,
    WorkSessionSource Source,
    string Note);
