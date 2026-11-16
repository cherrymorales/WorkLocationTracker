namespace WorkLocationTracker.Core.Models;

public sealed record WorkSessionRecord(
    string Id,
    DateTimeOffset StartTimeUtc,
    DateTimeOffset EndTimeUtc,
    int BreakMinutes,
    IReadOnlyDictionary<string, int> ActiveSecondsByNetwork,
    LocationType? LocationOverride,
    WorkSessionStatus Status,
    WorkSessionSource Source,
    string Note)
{
    public int TrackedActiveSeconds => ActiveSecondsByNetwork.Values.Sum();

    public int EffectiveWorkSeconds => Status == WorkSessionStatus.Confirmed
        ? Math.Max(0, checked((int)Math.Round((EndTimeUtc - StartTimeUtc).TotalSeconds)) - checked(BreakMinutes * 60))
        : TrackedActiveSeconds;
}
