using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Core.Reporting;

public sealed class UsageReportBuilder
{
    private readonly TimeZoneInfo _timeZone;

    public UsageReportBuilder(TimeZoneInfo? timeZone = null)
    {
        _timeZone = timeZone ?? TimeZoneInfo.Local;
    }

    public UsageReport Build(
        DateOnly from,
        DateOnly to,
        decimal hourlyRate,
        IReadOnlyList<DailyUsageRecord> dailyUsage,
        IReadOnlyList<NetworkLocation> networks,
        IReadOnlyList<ManualAdjustment> adjustments)
    {
        if (to < from)
        {
            throw new ArgumentException("The report end date must not be earlier than its start.", nameof(to));
        }

        if (hourlyRate < 0 || hourlyRate > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(hourlyRate), "The hourly rate must be between $0 and $100.");
        }

        var locationByNetwork = networks.ToDictionary(
            network => network.NetworkId,
            network => network.LocationType,
            StringComparer.Ordinal);

        var mutableDays = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(offset => from.AddDays(offset))
            .ToDictionary(date => date, _ => new MutableDay());
        var sessionReports = new List<WorkSessionReport>();

        foreach (var usage in dailyUsage)
        {
            if (!mutableDays.TryGetValue(usage.WorkDate, out var day))
            {
                continue;
            }

            if (usage.WorkSessions is not null)
            {
                foreach (var session in usage.WorkSessions.OrderBy(item => item.StartTimeUtc))
                {
                    AddSessionToDay(day, session, locationByNetwork);
                    var localStart = TimeZoneInfo.ConvertTime(session.StartTimeUtc, _timeZone);
                    var localEnd = TimeZoneInfo.ConvertTime(session.EndTimeUtc, _timeZone);
                    day.SetActivityWindow(localStart, localEnd);
                    sessionReports.Add(new WorkSessionReport(
                        usage.WorkDate,
                        session.Id,
                        localStart,
                        localEnd,
                        session.BreakMinutes,
                        session.EffectiveWorkSeconds / 3600d,
                        GetSessionLocation(session, locationByNetwork),
                        session.Status,
                        session.Source,
                        session.Note ?? string.Empty));
                }
            }
            else
            {
                foreach (var networkUsage in usage.ActiveSecondsByNetwork)
                {
                    var location = locationByNetwork.GetValueOrDefault(networkUsage.Key, LocationType.Unknown);
                    day.AddTracked(location, networkUsage.Value);
                }

                day.SetActivityWindow(
                    usage.StartTimeUtc is null ? null : TimeZoneInfo.ConvertTime(usage.StartTimeUtc.Value, _timeZone),
                    usage.EndTimeUtc is null ? null : TimeZoneInfo.ConvertTime(usage.EndTimeUtc.Value, _timeZone));
            }
        }

        foreach (var adjustment in adjustments)
        {
            if (mutableDays.TryGetValue(adjustment.WorkDate, out var day))
            {
                day.AddAdjustment(adjustment.LocationType, adjustment.Minutes);
            }
        }

        var days = mutableDays
            .OrderBy(pair => pair.Key)
            .Select(pair => pair.Value.ToReport(pair.Key))
            .ToArray();

        return new UsageReport(
            from,
            to,
            hourlyRate,
            TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _timeZone),
            days,
            networks,
            adjustments,
            sessionReports);
    }

    private static void AddSessionToDay(
        MutableDay day,
        WorkSessionRecord session,
        IReadOnlyDictionary<string, LocationType> locationByNetwork)
    {
        var effectiveSeconds = session.EffectiveWorkSeconds;
        if (effectiveSeconds <= 0)
        {
            return;
        }

        if (session.LocationOverride is { } locationOverride)
        {
            day.AddTracked(locationOverride, effectiveSeconds);
            return;
        }

        var trackedSeconds = session.TrackedActiveSeconds;
        if (trackedSeconds <= 0)
        {
            day.AddTracked(LocationType.Unknown, effectiveSeconds);
            return;
        }

        var groups = session.ActiveSecondsByNetwork
            .GroupBy(pair => locationByNetwork.GetValueOrDefault(pair.Key, LocationType.Unknown))
            .Select(group => new { Location = group.Key, Seconds = group.Sum(pair => pair.Value) })
            .Where(group => group.Seconds > 0)
            .ToArray();
        var remaining = effectiveSeconds;
        for (var index = 0; index < groups.Length; index++)
        {
            var seconds = index == groups.Length - 1
                ? remaining
                : Math.Min(remaining, (int)Math.Round(effectiveSeconds * groups[index].Seconds / (double)trackedSeconds));
            day.AddTracked(groups[index].Location, seconds);
            remaining -= seconds;
        }
    }

    private static string GetSessionLocation(
        WorkSessionRecord session,
        IReadOnlyDictionary<string, LocationType> locationByNetwork)
    {
        if (session.LocationOverride is { } locationOverride)
        {
            return DisplayLocation(locationOverride);
        }

        var locations = session.ActiveSecondsByNetwork
            .Where(pair => pair.Value > 0)
            .Select(pair => locationByNetwork.GetValueOrDefault(pair.Key, LocationType.Unknown))
            .Distinct()
            .ToArray();
        return locations.Length switch
        {
            0 => "Unclassified",
            1 => DisplayLocation(locations[0]),
            _ => "Mixed"
        };
    }

    private static string DisplayLocation(LocationType locationType) =>
        locationType == LocationType.Unknown ? "Unclassified" : locationType.ToString();

    private sealed class MutableDay
    {
        public int HomeTrackedSeconds { get; private set; }
        public int OfficeTrackedSeconds { get; private set; }
        public int OtherTrackedSeconds { get; private set; }
        public int UnknownTrackedSeconds { get; private set; }
        public int HomeAdjustmentMinutes { get; private set; }
        public int OfficeAdjustmentMinutes { get; private set; }
        public int OtherAdjustmentMinutes { get; private set; }
        public DateTimeOffset? StartTime { get; private set; }
        public DateTimeOffset? EndTime { get; private set; }

        public void AddTracked(LocationType locationType, int seconds)
        {
            switch (locationType)
            {
                case LocationType.Home:
                    HomeTrackedSeconds += seconds;
                    break;
                case LocationType.Office:
                    OfficeTrackedSeconds += seconds;
                    break;
                case LocationType.Other:
                    OtherTrackedSeconds += seconds;
                    break;
                default:
                    UnknownTrackedSeconds += seconds;
                    break;
            }
        }

        public void AddAdjustment(LocationType locationType, int minutes)
        {
            switch (locationType)
            {
                case LocationType.Home:
                    HomeAdjustmentMinutes += minutes;
                    break;
                case LocationType.Office:
                    OfficeAdjustmentMinutes += minutes;
                    break;
                case LocationType.Other:
                    OtherAdjustmentMinutes += minutes;
                    break;
            }
        }

        public void SetActivityWindow(DateTimeOffset? startTime, DateTimeOffset? endTime)
        {
            if (startTime is not null && (StartTime is null || startTime < StartTime))
            {
                StartTime = startTime;
            }

            if (endTime is not null && (EndTime is null || endTime > EndTime))
            {
                EndTime = endTime;
            }
        }

        public DailyUsageReport ToReport(DateOnly date) => new(
            date,
            HomeTrackedSeconds,
            OfficeTrackedSeconds,
            OtherTrackedSeconds,
            UnknownTrackedSeconds,
            HomeAdjustmentMinutes,
            OfficeAdjustmentMinutes,
            OtherAdjustmentMinutes,
            StartTime,
            EndTime);
    }
}
