using System.ComponentModel.DataAnnotations;

namespace WorkLocationTracker.Service;

public sealed class TrackingOptions
{
    public const string SectionName = "Tracking";

    [Range(15, 3600)]
    public int SampleIntervalSeconds { get; init; } = 60;

    [Range(1, 120)]
    public int IdleThresholdMinutes { get; init; } = 5;

    [Range(30, 7200)]
    public int MaximumGapSeconds { get; init; } = 150;
}

