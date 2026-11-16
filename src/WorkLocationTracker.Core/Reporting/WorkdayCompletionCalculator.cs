namespace WorkLocationTracker.Core.Reporting;

public enum TargetCompletionStatus
{
    NotApplicable,
    AwaitingFirstSession,
    Estimated,
    Completed
}

public sealed record TargetCompletionEstimate(
    TargetCompletionStatus Status,
    DateTimeOffset? Time);

public static class WorkdayCompletionCalculator
{
    public static TargetCompletionEstimate Calculate(
        DateOnly date,
        int targetMinutes,
        IReadOnlyCollection<WorkSessionReport> sessions,
        DateTimeOffset now)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(targetMinutes);
        ArgumentNullException.ThrowIfNull(sessions);

        if (targetMinutes == 0)
        {
            return new TargetCompletionEstimate(TargetCompletionStatus.NotApplicable, null);
        }

        var orderedSessions = sessions
            .Where(session => session.Date == date && session.WorkHours > 0)
            .OrderBy(session => session.StartTime)
            .ToArray();
        if (orderedSessions.Length == 0)
        {
            return new TargetCompletionEstimate(TargetCompletionStatus.AwaitingFirstSession, null);
        }

        var accumulatedMinutes = 0d;
        foreach (var session in orderedSessions)
        {
            var sessionMinutes = session.WorkHours * 60;
            var remainingMinutes = targetMinutes - accumulatedMinutes;
            if (sessionMinutes >= remainingMinutes)
            {
                var completionTime = session.StartTime.AddMinutes(
                    remainingMinutes + session.BreakMinutes);
                return new TargetCompletionEstimate(
                    TargetCompletionStatus.Completed,
                    completionTime > session.EndTime ? session.EndTime : completionTime);
            }

            accumulatedMinutes += sessionMinutes;
        }

        var progress = WorkdayTargetCalculator.Calculate(
            orderedSessions.Sum(session => session.WorkHours),
            targetMinutes);
        var latestSessionEnd = orderedSessions.Max(session => session.EndTime);
        var anchor = DateOnly.FromDateTime(now.DateTime) == date && now > latestSessionEnd
            ? now
            : latestSessionEnd;

        return new TargetCompletionEstimate(
            TargetCompletionStatus.Estimated,
            anchor.AddMinutes(progress.RemainingMinutes));
    }
}
