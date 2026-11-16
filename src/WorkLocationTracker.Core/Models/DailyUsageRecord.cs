namespace WorkLocationTracker.Core.Models;

public sealed record DailyUsageRecord(
    DateOnly WorkDate,
    DateTimeOffset? StartTimeUtc,
    DateTimeOffset? EndTimeUtc,
    IReadOnlyDictionary<string, int> ActiveSecondsByNetwork,
    int IdleSeconds,
    string MachineName,
    IReadOnlyList<WorkSessionRecord>? WorkSessions = null);
