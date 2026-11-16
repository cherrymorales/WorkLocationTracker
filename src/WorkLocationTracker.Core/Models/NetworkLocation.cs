namespace WorkLocationTracker.Core.Models;

public sealed record NetworkLocation(
    string NetworkId,
    string NetworkName,
    LocationType LocationType,
    DateTimeOffset LastSeenUtc,
    DateTimeOffset? UpdatedAtUtc);

