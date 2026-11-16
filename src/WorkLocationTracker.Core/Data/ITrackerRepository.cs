using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Core.Data;

public interface ITrackerRepository
{
    Task InitialiseAsync(CancellationToken cancellationToken = default);

    Task<WorkScheduleSettings> GetWorkScheduleSettingsAsync(
        CancellationToken cancellationToken = default);

    Task SaveWorkScheduleSettingsAsync(
        WorkScheduleSettings settings,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicHoliday>> GetPublicHolidaysAsync(
        CancellationToken cancellationToken = default);

    Task SavePublicHolidayAsync(
        PublicHoliday publicHoliday,
        CancellationToken cancellationToken = default);

    Task UpdatePublicHolidayAsync(
        DateOnly originalDate,
        PublicHoliday publicHoliday,
        CancellationToken cancellationToken = default);

    Task DeletePublicHolidayAsync(
        DateOnly date,
        CancellationToken cancellationToken = default);

    Task AddUsageSampleAsync(UsageSample sample, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyUsageRecord>> GetDailyUsageAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    Task UpsertWorkSessionAsync(
        DateOnly workDate,
        WorkSessionRecord session,
        CancellationToken cancellationToken = default);

    Task DeleteWorkSessionAsync(
        DateOnly workDate,
        string sessionId,
        CancellationToken cancellationToken = default);

    Task ConfirmWorkSessionsAsync(
        DateOnly workDate,
        CancellationToken cancellationToken = default);

    Task ReplaceWorkSessionsAsync(
        DateOnly workDate,
        IReadOnlyCollection<string> sessionIds,
        IReadOnlyList<WorkSessionRecord> replacements,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NetworkLocation>> GetNetworkLocationsAsync(
        CancellationToken cancellationToken = default);

    Task<NetworkLocation?> GetLatestNetworkAsync(CancellationToken cancellationToken = default);

    Task SetNetworkLocationAsync(
        string networkId,
        LocationType locationType,
        CancellationToken cancellationToken = default);

    Task<int> GetNetworkUsageDayCountAsync(
        string networkId,
        CancellationToken cancellationToken = default);

    Task DeleteNetworkAsync(
        string networkId,
        CancellationToken cancellationToken = default);

    Task AddManualAdjustmentAsync(
        DateOnly workDate,
        LocationType locationType,
        int minutes,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManualAdjustment>> GetManualAdjustmentsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}
