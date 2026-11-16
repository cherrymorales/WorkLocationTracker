using Microsoft.Data.Sqlite;
using WorkLocationTracker.Core;
using WorkLocationTracker.Core.Data;
using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Tests;

public sealed class TrackerRepositoryTests : IDisposable
{
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        "WorkLocationTracker.Tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Repository_PersistsWorkScheduleAndPublicHolidays()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();

        Assert.Equal(WorkScheduleSettings.Default, await repository.GetWorkScheduleSettingsAsync());

        var schedule = new WorkScheduleSettings(
            8 * 60,
            WorkingDays.Tuesday | WorkingDays.Wednesday | WorkingDays.Saturday);
        await repository.SaveWorkScheduleSettingsAsync(schedule);
        Assert.Equal(schedule, await repository.GetWorkScheduleSettingsAsync());

        var holidayDate = new DateOnly(2026, 10, 5);
        await repository.SavePublicHolidayAsync(new PublicHoliday(holidayDate, "Labour Day"));
        await repository.SavePublicHolidayAsync(new PublicHoliday(
            holidayDate,
            "Medical appointment",
            HolidayType.SickLeave,
            LeaveMinutes: 180));

        var holiday = Assert.Single(await repository.GetPublicHolidaysAsync());
        Assert.Equal(holidayDate, holiday.Date);
        Assert.Equal("Medical appointment", holiday.Name);
        Assert.Equal(HolidayType.SickLeave, holiday.Type);
        Assert.Equal(180, holiday.LeaveMinutes);

        var updatedDate = holidayDate.AddDays(1);
        await repository.UpdatePublicHolidayAsync(
            holidayDate,
            new PublicHoliday(
                updatedDate,
                "Annual leave",
                HolidayType.AnnualLeave,
                LeaveMinutes: 240));

        holiday = Assert.Single(await repository.GetPublicHolidaysAsync());
        Assert.Equal(updatedDate, holiday.Date);
        Assert.Equal(HolidayType.AnnualLeave, holiday.Type);
        Assert.Equal(240, holiday.LeaveMinutes);

        await repository.DeletePublicHolidayAsync(updatedDate);
        Assert.Empty(await repository.GetPublicHolidaysAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.DeletePublicHolidayAsync(updatedDate));
    }

    [Fact]
    public async Task Repository_MigratesExistingHolidaysAsFullDayPublicHolidays()
    {
        var paths = new AppPaths(_temporaryDirectory);
        Directory.CreateDirectory(paths.DataDirectory);
        await using (var connection = new SqliteConnection($"Data Source={paths.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE PublicHolidays (
                    WorkDate TEXT NOT NULL PRIMARY KEY,
                    Name TEXT NOT NULL CHECK (length(Name) <= 120)
                );
                INSERT INTO PublicHolidays (WorkDate, Name)
                VALUES ('2026-12-25', 'Christmas Day');
                """;
            await command.ExecuteNonQueryAsync();
        }

        var repository = new TrackerRepository(paths);
        await repository.InitialiseAsync();

        var holiday = Assert.Single(await repository.GetPublicHolidaysAsync());
        Assert.Equal(HolidayType.PublicHoliday, holiday.Type);
        Assert.Equal(0, holiday.LeaveMinutes);
    }

    [Fact]
    public async Task Repository_PersistsSamplesClassificationsAndAdjustments()
    {
        var paths = new AppPaths(_temporaryDirectory);
        var repository = new TrackerRepository(paths);
        await repository.InitialiseAsync();
        var workDate = new DateOnly(2026, 8, 17);
        var start = LocalTime(workDate, 8, 0);
        var firstSample = new UsageSample(
            0,
            start,
            start.AddMinutes(1),
            60,
            0,
            "network-one",
            "Home Wi-Fi",
            "TEST-MACHINE");
        var secondSample = firstSample with
        {
            PeriodStartUtc = start.AddMinutes(1),
            PeriodEndUtc = start.AddMinutes(2),
            ActiveSeconds = 30,
            IdleSeconds = 30
        };

        await repository.AddUsageSampleAsync(firstSample);
        await repository.AddUsageSampleAsync(firstSample);
        await repository.AddUsageSampleAsync(secondSample);
        await repository.SetNetworkLocationAsync("network-one", LocationType.Home);
        await repository.AddManualAdjustmentAsync(
            workDate,
            LocationType.Home,
            30,
            "Online meeting");

        var dailyUsage = await repository.GetDailyUsageAsync(workDate, workDate);
        var networks = await repository.GetNetworkLocationsAsync();
        var adjustments = await repository.GetManualAdjustmentsAsync(
            workDate,
            workDate);

        var day = Assert.Single(dailyUsage);
        Assert.Equal(90, day.ActiveSecondsByNetwork["network-one"]);
        Assert.Equal(30, day.IdleSeconds);
        Assert.Equal(start.ToUniversalTime(), day.StartTimeUtc);
        Assert.Equal(start.AddMinutes(2).ToUniversalTime(), day.EndTimeUtc);
        var session = Assert.Single(day.WorkSessions!);
        Assert.Equal(90, session.TrackedActiveSeconds);
        Assert.Equal(WorkSessionStatus.Suggested, session.Status);
        Assert.Equal(LocationType.Home, Assert.Single(networks).LocationType);
        Assert.Equal(30, Assert.Single(adjustments).Minutes);

        await using var connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM DailyUsage;";
        Assert.Equal(1L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Repository_DeletesNetworkClassificationWithoutDeletingLinkedDailyUsage()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();
        var date = new DateOnly(2026, 8, 20);
        var start = LocalTime(date, 9, 0);
        await repository.AddUsageSampleAsync(new UsageSample(
            0,
            start,
            start.AddMinutes(1),
            60,
            0,
            "network-to-delete",
            "Old Wi-Fi",
            "TEST-MACHINE"));
        await repository.SetNetworkLocationAsync("network-to-delete", LocationType.Home);

        Assert.Equal(1, await repository.GetNetworkUsageDayCountAsync("network-to-delete"));
        await repository.DeleteNetworkAsync("network-to-delete");

        Assert.Empty(await repository.GetNetworkLocationsAsync());
        var dailyUsage = Assert.Single(await repository.GetDailyUsageAsync(date, date));
        Assert.Equal(60, dailyUsage.ActiveSecondsByNetwork["network-to-delete"]);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            repository.DeleteNetworkAsync("network-to-delete"));
    }

    [Fact]
    public async Task Repository_CoalescesNearbyActivityAndAllowsDailyConfirmation()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();
        var date = new DateOnly(2026, 8, 18);
        var start = LocalTime(date, 8, 0);

        foreach (var offsetMinutes in new[] { 0, 10, 60 })
        {
            await repository.AddUsageSampleAsync(new UsageSample(
                0,
                start.AddMinutes(offsetMinutes),
                start.AddMinutes(offsetMinutes + 1),
                60,
                0,
                "home",
                "Home Wi-Fi",
                "TEST-MACHINE"));
        }

        var beforeConfirmation = Assert.Single(await repository.GetDailyUsageAsync(date, date));
        Assert.Collection(
            beforeConfirmation.WorkSessions!,
            first =>
            {
                Assert.Equal(120, first.TrackedActiveSeconds);
                Assert.Equal(9, first.BreakMinutes);
                Assert.Equal(WorkSessionStatus.Suggested, first.Status);
            },
            second => Assert.Equal(60, second.TrackedActiveSeconds));

        await repository.ConfirmWorkSessionsAsync(date);
        var confirmed = Assert.Single(await repository.GetDailyUsageAsync(date, date));
        Assert.All(confirmed.WorkSessions!, session => Assert.Equal(WorkSessionStatus.Confirmed, session.Status));
        Assert.Equal(120, confirmed.WorkSessions![0].EffectiveWorkSeconds);
    }

    [Fact]
    public async Task Repository_SplitsAnActivitySampleAtLocalMidnight()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();
        var firstDate = new DateOnly(2026, 8, 18);
        var start = LocalTime(firstDate, 23, 59).AddSeconds(30);

        await repository.AddUsageSampleAsync(new UsageSample(
            0,
            start,
            start.AddMinutes(1),
            60,
            0,
            "home",
            "Home Wi-Fi",
            "TEST-MACHINE"));

        var days = await repository.GetDailyUsageAsync(firstDate, firstDate.AddDays(1));
        Assert.Equal(2, days.Count);
        Assert.Equal(30, days[0].ActiveSecondsByNetwork["home"]);
        Assert.Equal(30, days[1].ActiveSecondsByNetwork["home"]);
        Assert.Equal(firstDate, days[0].WorkDate);
        Assert.Equal(firstDate.AddDays(1), days[1].WorkDate);
        Assert.Single(days[0].WorkSessions!);
        Assert.Single(days[1].WorkSessions!);
    }

    [Fact]
    public async Task Repository_PreservesConfirmedSessionDuringLaterSamplingAndRejectsOverlap()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();
        var date = new DateOnly(2026, 8, 20);
        var start = LocalTime(date, 8, 0);
        await repository.AddUsageSampleAsync(new UsageSample(
            0, start, start.AddMinutes(1), 60, 0, "home", "Home Wi-Fi", "TEST-MACHINE"));

        var automatic = Assert.Single(Assert.Single(await repository.GetDailyUsageAsync(date, date)).WorkSessions!);
        var confirmed = automatic with
        {
            EndTimeUtc = start.AddHours(2).ToUniversalTime(),
            BreakMinutes = 30,
            LocationOverride = LocationType.Home,
            Status = WorkSessionStatus.Confirmed
        };
        await repository.UpsertWorkSessionAsync(date, confirmed);

        await repository.AddUsageSampleAsync(new UsageSample(
            0, start.AddHours(1), start.AddHours(1).AddMinutes(1), 60, 0,
            "home", "Home Wi-Fi", "TEST-MACHINE"));
        var afterSampling = Assert.Single(await repository.GetDailyUsageAsync(date, date));
        Assert.Single(afterSampling.WorkSessions!);

        var overlapping = new WorkSessionRecord(
            Guid.NewGuid().ToString("N"),
            start.AddHours(1).ToUniversalTime(),
            start.AddHours(3).ToUniversalTime(),
            0,
            new Dictionary<string, int>(),
            LocationType.Home,
            WorkSessionStatus.Confirmed,
            WorkSessionSource.Manual,
            string.Empty);
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.UpsertWorkSessionAsync(date, overlapping));

        var separate = overlapping with
        {
            Id = Guid.NewGuid().ToString("N"),
            StartTimeUtc = start.AddHours(2).AddMinutes(30).ToUniversalTime(),
            EndTimeUtc = start.AddHours(3).ToUniversalTime()
        };
        await repository.UpsertWorkSessionAsync(date, separate);
        Assert.Equal(2, Assert.Single(await repository.GetDailyUsageAsync(date, date)).WorkSessions!.Count);
        await repository.DeleteWorkSessionAsync(date, separate.Id);
        var remaining = Assert.Single(Assert.Single(await repository.GetDailyUsageAsync(date, date)).WorkSessions!);

        var firstHalf = remaining with
        {
            EndTimeUtc = start.AddHours(1).ToUniversalTime(),
            BreakMinutes = 0
        };
        var secondHalf = remaining with
        {
            Id = Guid.NewGuid().ToString("N"),
            StartTimeUtc = start.AddHours(1).AddMinutes(30).ToUniversalTime(),
            BreakMinutes = 0
        };
        await repository.ReplaceWorkSessionsAsync(
            date,
            new[] { remaining.Id },
            new[] { firstHalf, secondHalf });
        Assert.Equal(2, Assert.Single(await repository.GetDailyUsageAsync(date, date)).WorkSessions!.Count);
    }

    [Fact]
    public async Task Initialise_MigratesMinuteSamplesIntoOneDailyRecord()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var paths = new AppPaths(_temporaryDirectory);
        var workDate = new DateOnly(2026, 8, 17);
        var start = LocalTime(workDate, 8, 0);

        await using (var connection = new SqliteConnection($"Data Source={paths.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var schemaCommand = connection.CreateCommand();
            schemaCommand.CommandText = """
                CREATE TABLE UsageSamples (
                    Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    PeriodStartUtc TEXT NOT NULL,
                    PeriodEndUtc TEXT NOT NULL,
                    ActiveSeconds INTEGER NOT NULL,
                    IdleSeconds INTEGER NOT NULL,
                    NetworkId TEXT NOT NULL,
                    NetworkName TEXT NOT NULL,
                    MachineName TEXT NOT NULL
                );
                """;
            await schemaCommand.ExecuteNonQueryAsync();

            for (var minute = 0; minute < 2; minute++)
            {
                await using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = """
                    INSERT INTO UsageSamples
                        (PeriodStartUtc, PeriodEndUtc, ActiveSeconds, IdleSeconds,
                         NetworkId, NetworkName, MachineName)
                    VALUES ($start, $end, 60, 0, 'network-one', 'Home Wi-Fi', 'TEST-MACHINE');
                    """;
                insertCommand.Parameters.AddWithValue("$start", start.AddMinutes(minute).ToUniversalTime().ToString("O"));
                insertCommand.Parameters.AddWithValue("$end", start.AddMinutes(minute + 1).ToUniversalTime().ToString("O"));
                await insertCommand.ExecuteNonQueryAsync();
            }
        }

        var repository = new TrackerRepository(paths);
        await repository.InitialiseAsync();

        var day = Assert.Single(await repository.GetDailyUsageAsync(workDate, workDate));
        Assert.Equal(120, day.ActiveSecondsByNetwork["network-one"]);
        Assert.Equal(start.ToUniversalTime(), day.StartTimeUtc);
        Assert.Equal(start.AddMinutes(2).ToUniversalTime(), day.EndTimeUtc);

        var backupPath = Assert.Single(Directory.GetFiles(
            _temporaryDirectory,
            "work-location-tracker.pre-v1.3.0-*.db"));
        await using (var backupConnection = new SqliteConnection($"Data Source={backupPath};Mode=ReadOnly"))
        {
            await backupConnection.OpenAsync();
            await using var backupCommand = backupConnection.CreateCommand();
            backupCommand.CommandText = "SELECT COUNT(*) FROM UsageSamples;";
            Assert.Equal(2L, (long)(await backupCommand.ExecuteScalarAsync())!);
        }

        await using var verificationConnection = new SqliteConnection($"Data Source={paths.DatabasePath}");
        await verificationConnection.OpenAsync();
        await using var verificationCommand = verificationConnection.CreateCommand();
        verificationCommand.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM DailyUsage),
                (SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'UsageSamples'),
                (SELECT COUNT(*) FROM pragma_table_info('DailyUsage')
                    WHERE name IN ('StartTimeUtc', 'EndTimeUtc'));
            """;
        await using var reader = await verificationCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(0, reader.GetInt32(1));
        Assert.Equal(2, reader.GetInt32(2));
        await reader.DisposeAsync();

        verificationCommand.CommandText = "PRAGMA freelist_count;";
        Assert.Equal(0L, (long)(await verificationCommand.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Initialise_MigratesDailySummaryIntoAnImportedReviewSession()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        var paths = new AppPaths(_temporaryDirectory);
        var date = new DateOnly(2026, 8, 19);
        var start = LocalTime(date, 7, 0).ToUniversalTime();
        var end = LocalTime(date, 17, 0).ToUniversalTime();

        await using (var connection = new SqliteConnection($"Data Source={paths.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE DailyUsage (
                    WorkDate TEXT NOT NULL PRIMARY KEY,
                    StartTimeUtc TEXT NULL,
                    EndTimeUtc TEXT NULL,
                    ActiveSecondsByNetworkJson TEXT NOT NULL,
                    IdleSeconds INTEGER NOT NULL,
                    MachineName TEXT NOT NULL,
                    LastSampleEndUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL
                );
                INSERT INTO DailyUsage
                    (WorkDate, StartTimeUtc, EndTimeUtc, ActiveSecondsByNetworkJson,
                     IdleSeconds, MachineName, LastSampleEndUtc, UpdatedAtUtc)
                VALUES
                    ($date, $start, $end, '{"home":34200}', 1800,
                     'TEST-MACHINE', $end, $end);
                """;
            command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));
            command.Parameters.AddWithValue("$start", start.ToString("O"));
            command.Parameters.AddWithValue("$end", end.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        var repository = new TrackerRepository(paths);
        await repository.InitialiseAsync();

        var day = Assert.Single(await repository.GetDailyUsageAsync(date, date));
        var session = Assert.Single(day.WorkSessions!);
        Assert.Equal(WorkSessionSource.ImportedSummary, session.Source);
        Assert.Equal(WorkSessionStatus.Suggested, session.Status);
        Assert.Equal(30, session.BreakMinutes);
        Assert.Equal(34200, session.TrackedActiveSeconds);
        Assert.Single(Directory.GetFiles(_temporaryDirectory, "work-location-tracker.pre-v1.4.0-*.db"));

        await using var verificationConnection = new SqliteConnection($"Data Source={paths.DatabasePath}");
        await verificationConnection.OpenAsync();
        await using var verificationCommand = verificationConnection.CreateCommand();
        verificationCommand.CommandText = "SELECT COUNT(*) FROM pragma_table_info('DailyUsage') WHERE name = 'WorkSessionsJson';";
        Assert.Equal(1L, (long)(await verificationCommand.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task Repository_RejectsAdjustmentWithoutReason()
    {
        var repository = new TrackerRepository(new AppPaths(_temporaryDirectory));
        await repository.InitialiseAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => repository.AddManualAdjustmentAsync(
            new DateOnly(2026, 8, 17),
            LocationType.Home,
            15,
            " "));
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

    private static DateTimeOffset LocalTime(DateOnly date, int hour, int minute)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(new TimeOnly(hour, minute)), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
