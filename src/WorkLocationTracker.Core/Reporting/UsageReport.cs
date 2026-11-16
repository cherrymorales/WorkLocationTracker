using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Core.Reporting;

public sealed record UsageReport(
    DateOnly From,
    DateOnly To,
    decimal HourlyRate,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<DailyUsageReport> Days,
    IReadOnlyList<NetworkLocation> Networks,
    IReadOnlyList<ManualAdjustment> Adjustments,
    IReadOnlyList<WorkSessionReport>? Sessions = null)
{
    public IReadOnlyList<WorkSessionReport> DetailedSessions => Sessions ?? Array.Empty<WorkSessionReport>();

    public double TotalHomeHours => Days.Sum(day => day.HomeHours);

    public double TotalOfficeHours => Days.Sum(day => day.OfficeHours);

    public double TotalOtherHours => Days.Sum(day => day.OtherHours);

    public double TotalUnknownHours => Days.Sum(day => day.UnknownHours);

    public decimal EstimatedHomeValue =>
        decimal.Round((decimal)TotalHomeHours * HourlyRate, 2, MidpointRounding.AwayFromZero);
}
