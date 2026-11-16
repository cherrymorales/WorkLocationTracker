namespace WorkLocationTracker.Core.Models;

public sealed record UsageSample(
    long Id,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    int ActiveSeconds,
    int IdleSeconds,
    string NetworkId,
    string NetworkName,
    string MachineName);

