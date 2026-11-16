using WorkLocationTracker.Core;
using WorkLocationTracker.Core.Data;
using WorkLocationTracker.Core.Models;
using WorkLocationTracker.Core.Reporting;
using WorkLocationTracker.Monitor.ViewModels;
using Microsoft.Data.Sqlite;

namespace WorkLocationTracker.Tests;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "WorkLocationTracker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitialiseAsync_BuildsTodayDashboardFromWorkSessions()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();
        await repository.SaveWorkScheduleSettingsAsync(new WorkScheduleSettings(
            DailyTargetMinutes: 456,
            ScheduledDays: WorkingDays.All));
        var date = DateOnly.FromDateTime(DateTime.Today);
        var start = LocalTime(date, 8, 0);
        await repository.AddUsageSampleAsync(new UsageSample(
            0,
            start,
            start.AddMinutes(1),
            60,
            0,
            "home",
            "Home Wi-Fi",
            "TEST-MACHINE"));
        await repository.SetNetworkLocationAsync("home", LocationType.Home);

        var automatic = Assert.Single(
            Assert.Single(await repository.GetDailyUsageAsync(date, date)).WorkSessions!);
        await repository.UpsertWorkSessionAsync(date, automatic with
        {
            EndTimeUtc = start.AddHours(8).ToUniversalTime(),
            BreakMinutes = 30,
            LocationOverride = LocationType.Home,
            Status = WorkSessionStatus.Confirmed
        });

        var viewModel = new MainViewModel(repository, new UsageReportBuilder(), new ReportExporter());
        await viewModel.InitialiseAsync();

        Assert.Equal("7 h 30 min", viewModel.TodayTotalWorkHours);
        Assert.Equal("0 h 06 min remaining", viewModel.TodayTargetStatus);
        Assert.Equal("0 h 30 min", viewModel.TodayBreakTotal);
        Assert.StartsWith("0 / ", viewModel.WeekTargetDays, StringComparison.Ordinal);
        Assert.Equal("7 h 30 min", viewModel.WeekTotalHours);
        Assert.Equal("0 h 00 min", viewModel.WeekAdditionalHours);
        Assert.StartsWith("0 / ", viewModel.MonthTargetDays, StringComparison.Ordinal);
        Assert.Equal("7 h 30 min", viewModel.MonthTotalHours);
        Assert.Equal("0 h 00 min", viewModel.MonthAdditionalHours);
        Assert.Equal("0 h 00 min", viewModel.MonthlyAdditionalHours);
    }

    [Fact]
    public async Task MergeWorkSessions_MergesAllSelectedRowsAndCountsInterveningGapsAsBreaks()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();
        var date = new DateOnly(2026, 9, 4);
        var start = LocalTime(date, 8, 0);
        await repository.AddUsageSampleAsync(new UsageSample(
            0,
            start,
            start.AddMinutes(1),
            60,
            0,
            "home",
            "Home Wi-Fi",
            "TEST-MACHINE"));

        var original = Assert.Single(
            Assert.Single(await repository.GetDailyUsageAsync(date, date)).WorkSessions!);
        var sessions = new[]
        {
            ConfirmedSession(original.Id, start, start.AddHours(2), 10, "Morning work"),
            ConfirmedSession(Guid.NewGuid().ToString("N"), start.AddHours(2).AddMinutes(30), start.AddHours(4), 5, "Client work"),
            ConfirmedSession(Guid.NewGuid().ToString("N"), start.AddHours(5), start.AddHours(7), 0, "Morning work")
        };
        await repository.ReplaceWorkSessionsAsync(
            date,
            new[] { original.Id },
            sessions);

        var viewModel = new MainViewModel(repository, new UsageReportBuilder(), new ReportExporter())
        {
            DailyDetailDate = date.ToDateTime(TimeOnly.MinValue)
        };
        await viewModel.RefreshDailyDetailAsync();

        await viewModel.MergeWorkSessionsAsync(viewModel.WorkSessions.ToArray());

        var merged = Assert.Single(
            Assert.Single(await repository.GetDailyUsageAsync(date, date)).WorkSessions!);
        Assert.Equal(sessions[0].Id, merged.Id);
        Assert.Equal(start.ToUniversalTime(), merged.StartTimeUtc);
        Assert.Equal(start.AddHours(7).ToUniversalTime(), merged.EndTimeUtc);
        Assert.Equal(105, merged.BreakMinutes);
        Assert.Equal(LocationType.Home, merged.LocationOverride);
        Assert.Equal(WorkSessionStatus.Confirmed, merged.Status);
        Assert.Equal(WorkSessionSource.Manual, merged.Source);
        Assert.Equal("Morning work | Client work", merged.Note);
    }

    [Fact]
    public void DailyWorkSessionsHeading_UsesSelectedDateAndWeekday()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        var viewModel = new MainViewModel(repository, new UsageReportBuilder(), new ReportExporter())
        {
            DailyDetailDate = new DateTime(2026, 11, 8)
        };

        Assert.Equal("Daily work sessions - 8 November 2026, Sunday", viewModel.DailyWorkSessionsHeading);

        viewModel.DailyDetailDate = new DateTime(2026, 11, 9);

        Assert.Equal("Daily work sessions - 9 November 2026, Monday", viewModel.DailyWorkSessionsHeading);
    }

    [Fact]
    public async Task HolidayEditor_LoadsAndUpdatesTheSelectedRecord()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();
        var originalDate = new DateOnly(2026, 10, 12);
        await repository.SavePublicHolidayAsync(new PublicHoliday(
            originalDate,
            "Appointment",
            HolidayType.SickLeave,
            LeaveMinutes: 120));
        var viewModel = new MainViewModel(repository, new UsageReportBuilder(), new ReportExporter());
        await viewModel.InitialiseAsync();
        viewModel.SelectedPublicHoliday = Assert.Single(viewModel.PublicHolidays);

        viewModel.BeginEditSelectedPublicHoliday();

        Assert.True(viewModel.IsEditingPublicHoliday);
        Assert.Equal("Save changes", viewModel.HolidaySaveButtonText);
        Assert.Equal(HolidayType.SickLeave, viewModel.NewPublicHolidayType);
        Assert.Equal("2", viewModel.NewHolidayLeaveHoursText);
        Assert.Equal("0", viewModel.NewHolidayLeaveMinutesText);

        var updatedDate = originalDate.AddDays(1);
        viewModel.NewPublicHolidayDate = updatedDate.ToDateTime(TimeOnly.MinValue);
        viewModel.NewPublicHolidayName = "Annual leave";
        viewModel.NewPublicHolidayType = HolidayType.AnnualLeave;
        viewModel.NewHolidayLeaveHoursText = "3";
        viewModel.NewHolidayLeaveMinutesText = "30";
        await viewModel.SavePublicHolidayAsync();

        var updated = Assert.Single(await repository.GetPublicHolidaysAsync());
        Assert.Equal(updatedDate, updated.Date);
        Assert.Equal("Annual leave", updated.Name);
        Assert.Equal(HolidayType.AnnualLeave, updated.Type);
        Assert.Equal(210, updated.LeaveMinutes);
        Assert.False(viewModel.IsEditingPublicHoliday);
        Assert.Equal("Add date", viewModel.HolidaySaveButtonText);
    }

    private static WorkSessionRecord ConfirmedSession(
        string id,
        DateTimeOffset start,
        DateTimeOffset end,
        int breakMinutes,
        string note) =>
        new(
            id,
            start.ToUniversalTime(),
            end.ToUniversalTime(),
            breakMinutes,
            new Dictionary<string, int>(StringComparer.Ordinal),
            LocationType.Home,
            WorkSessionStatus.Confirmed,
            WorkSessionSource.Manual,
            note);

    private static DateTimeOffset LocalTime(DateOnly date, int hour, int minute)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }

    public void Dispose()
    {
        if (!_temporaryDirectory.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete a test directory outside the system temporary path.");
        }

        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }
}
