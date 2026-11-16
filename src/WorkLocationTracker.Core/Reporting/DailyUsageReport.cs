namespace WorkLocationTracker.Core.Reporting;

public sealed record DailyUsageReport(
    DateOnly Date,
    int HomeTrackedSeconds,
    int OfficeTrackedSeconds,
    int OtherTrackedSeconds,
    int UnknownTrackedSeconds,
    int HomeAdjustmentMinutes,
    int OfficeAdjustmentMinutes,
    int OtherAdjustmentMinutes,
    DateTimeOffset? StartTime = null,
    DateTimeOffset? EndTime = null)
{
    public double HomeHours => Math.Max(0, HomeTrackedSeconds / 3600d + HomeAdjustmentMinutes / 60d);

    public double OfficeHours => Math.Max(0, OfficeTrackedSeconds / 3600d + OfficeAdjustmentMinutes / 60d);

    public double OtherHours => Math.Max(0, OtherTrackedSeconds / 3600d + OtherAdjustmentMinutes / 60d);

    public double UnknownHours => UnknownTrackedSeconds / 3600d;
}
