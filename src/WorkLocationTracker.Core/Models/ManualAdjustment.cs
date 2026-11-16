namespace WorkLocationTracker.Core.Models;

public sealed record ManualAdjustment(
    long Id,
    DateOnly WorkDate,
    LocationType LocationType,
    int Minutes,
    string Reason,
    DateTimeOffset CreatedAtUtc);

