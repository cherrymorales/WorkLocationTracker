using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using WorkLocationTracker.Core.Models;

namespace WorkLocationTracker.Core.Data;

public sealed class TrackerRepository : ITrackerRepository
{
    private const string DateTimeFormat = "O";
    private static readonly TimeSpan SuggestedSessionGap = TimeSpan.FromMinutes(30);
    private readonly AppPaths _paths;

    public TrackerRepository(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task InitialiseAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DataDirectory);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode = WAL;
            PRAGMA journal_size_limit = 4194304;
            PRAGMA wal_autocheckpoint = 250;
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS Networks (
                NetworkId TEXT NOT NULL PRIMARY KEY,
                NetworkName TEXT NOT NULL,
                LocationType INTEGER NOT NULL DEFAULT 0
                    CHECK (LocationType BETWEEN 0 AND 3),
                LastSeenUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS DailyUsage (
                WorkDate TEXT NOT NULL PRIMARY KEY,
                StartTimeUtc TEXT NULL,
                EndTimeUtc TEXT NULL,
                ActiveSecondsByNetworkJson TEXT NOT NULL,
                IdleSeconds INTEGER NOT NULL CHECK (IdleSeconds >= 0),
                MachineName TEXT NOT NULL,
                LastSampleEndUtc TEXT NOT NULL,
                UpdatedAtUtc TEXT NOT NULL,
                WorkSessionsJson TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS ManualAdjustments (
                Id INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                WorkDate TEXT NOT NULL,
                LocationType INTEGER NOT NULL CHECK (LocationType BETWEEN 1 AND 3),
                Minutes INTEGER NOT NULL CHECK (Minutes BETWEEN -1440 AND 1440 AND Minutes <> 0),
                Reason TEXT NOT NULL CHECK (length(Reason) BETWEEN 1 AND 500),
                CreatedAtUtc TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS IX_ManualAdjustments_WorkDate
                ON ManualAdjustments (WorkDate);

            CREATE TABLE IF NOT EXISTS WorkScheduleSettings (
                Id INTEGER NOT NULL PRIMARY KEY CHECK (Id = 1),
                DailyTargetMinutes INTEGER NOT NULL CHECK (DailyTargetMinutes BETWEEN 1 AND 1440),
                WorkingDaysMask INTEGER NOT NULL CHECK (WorkingDaysMask BETWEEN 1 AND 127)
            );

            INSERT OR IGNORE INTO WorkScheduleSettings
                (Id, DailyTargetMinutes, WorkingDaysMask)
            VALUES
                (1, 456, 62);

            CREATE TABLE IF NOT EXISTS PublicHolidays (
                WorkDate TEXT NOT NULL PRIMARY KEY,
                Name TEXT NOT NULL CHECK (length(Name) <= 120),
                Type INTEGER NOT NULL DEFAULT 0 CHECK (Type BETWEEN 0 AND 2),
                LeaveMinutes INTEGER NOT NULL DEFAULT 0 CHECK (LeaveMinutes BETWEEN 0 AND 1440)
            );
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        await MigrateHolidayDetailsAsync(connection, cancellationToken);
        await MigrateWorkSessionsAsync(connection, cancellationToken);

        if (await MigrateLegacyUsageSamplesAsync(connection, cancellationToken))
        {
            await VacuumDatabaseAsync(connection, cancellationToken);
        }

        await CheckpointDatabaseAsync(connection, cancellationToken);
    }

    public async Task<WorkScheduleSettings> GetWorkScheduleSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DailyTargetMinutes, WorkingDaysMask
            FROM WorkScheduleSettings
            WHERE Id = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return WorkScheduleSettings.Default;
        }

        return new WorkScheduleSettings(
            reader.GetInt32(0),
            (WorkingDays)reader.GetInt32(1));
    }

    public async Task SaveWorkScheduleSettingsAsync(
        WorkScheduleSettings settings,
        CancellationToken cancellationToken = default)
    {
        ValidateWorkScheduleSettings(settings);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO WorkScheduleSettings (Id, DailyTargetMinutes, WorkingDaysMask)
            VALUES (1, $dailyTargetMinutes, $workingDaysMask)
            ON CONFLICT (Id) DO UPDATE SET
                DailyTargetMinutes = excluded.DailyTargetMinutes,
                WorkingDaysMask = excluded.WorkingDaysMask;
            """;
        command.Parameters.AddWithValue("$dailyTargetMinutes", settings.DailyTargetMinutes);
        command.Parameters.AddWithValue("$workingDaysMask", (int)settings.ScheduledDays);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicHoliday>> GetPublicHolidaysAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<PublicHoliday>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT WorkDate, Name, Type, LeaveMinutes
            FROM PublicHolidays
            ORDER BY WorkDate;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PublicHoliday(
                DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                reader.GetString(1),
                (HolidayType)reader.GetInt32(2),
                reader.GetInt32(3)));
        }

        return results;
    }

    public async Task SavePublicHolidayAsync(
        PublicHoliday publicHoliday,
        CancellationToken cancellationToken = default)
    {
        var name = ValidatePublicHoliday(publicHoliday);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO PublicHolidays (WorkDate, Name, Type, LeaveMinutes)
            VALUES ($workDate, $name, $type, $leaveMinutes)
            ON CONFLICT (WorkDate) DO UPDATE SET
                Name = excluded.Name,
                Type = excluded.Type,
                LeaveMinutes = excluded.LeaveMinutes;
            """;
        command.Parameters.AddWithValue("$workDate", ToDatabaseDate(publicHoliday.Date));
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$type", (int)publicHoliday.Type);
        command.Parameters.AddWithValue("$leaveMinutes", publicHoliday.LeaveMinutes);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdatePublicHolidayAsync(
        DateOnly originalDate,
        PublicHoliday publicHoliday,
        CancellationToken cancellationToken = default)
    {
        var name = ValidatePublicHoliday(publicHoliday);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE PublicHolidays
            SET WorkDate = $workDate,
                Name = $name,
                Type = $type,
                LeaveMinutes = $leaveMinutes
            WHERE WorkDate = $originalWorkDate;
            """;
        command.Parameters.AddWithValue("$originalWorkDate", ToDatabaseDate(originalDate));
        command.Parameters.AddWithValue("$workDate", ToDatabaseDate(publicHoliday.Date));
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$type", (int)publicHoliday.Type);
        command.Parameters.AddWithValue("$leaveMinutes", publicHoliday.LeaveMinutes);

        try
        {
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new InvalidOperationException("The selected Holiday no longer exists.");
            }
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode == 19 && originalDate != publicHoliday.Date)
        {
            throw new InvalidOperationException("Another Holiday or leave record already uses the new date.", exception);
        }
    }

    public async Task DeletePublicHolidayAsync(
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM PublicHolidays WHERE WorkDate = $workDate;";
        command.Parameters.AddWithValue("$workDate", ToDatabaseDate(date));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("The selected public holiday no longer exists.");
        }
    }

    public async Task AddUsageSampleAsync(
        UsageSample sample,
        CancellationToken cancellationToken = default)
    {
        ValidateSample(sample);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);

        await using (var networkCommand = connection.CreateCommand())
        {
            networkCommand.Transaction = (SqliteTransaction)transaction;
            networkCommand.CommandText = """
                INSERT INTO Networks (NetworkId, NetworkName, LocationType, LastSeenUtc)
                VALUES ($networkId, $networkName, 0, $lastSeenUtc)
                ON CONFLICT (NetworkId) DO UPDATE SET
                    NetworkName = excluded.NetworkName,
                    LastSeenUtc = excluded.LastSeenUtc;
                """;
            networkCommand.Parameters.AddWithValue("$networkId", sample.NetworkId);
            networkCommand.Parameters.AddWithValue("$networkName", sample.NetworkName);
            networkCommand.Parameters.AddWithValue("$lastSeenUtc", ToDatabaseDate(sample.PeriodEndUtc));
            await networkCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var dailySample in SplitByLocalDate(sample))
        {
            var workDate = GetLocalWorkDate(dailySample.PeriodStartUtc);
            var dailyUsage = await ReadDailyUsageStateAsync(connection, transaction, workDate, cancellationToken)
                ?? new DailyUsageState(workDate, machineName: dailySample.MachineName);

            if (dailyUsage.LastSampleEndUtc is null || dailySample.PeriodEndUtc > dailyUsage.LastSampleEndUtc)
            {
                dailyUsage.AddSample(dailySample);
                await UpsertDailyUsageAsync(connection, transaction, dailyUsage, cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DailyUsageRecord>> GetDailyUsageAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ArgumentException("The report end date must not be earlier than its start.", nameof(to));
        }

        var results = new List<DailyUsageRecord>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT WorkDate, StartTimeUtc, EndTimeUtc, ActiveSecondsByNetworkJson,
                   IdleSeconds, MachineName, LastSampleEndUtc, WorkSessionsJson
            FROM DailyUsage
            WHERE WorkDate BETWEEN $from AND $to
            ORDER BY WorkDate;
            """;
        command.Parameters.AddWithValue("$from", ToDatabaseDate(from));
        command.Parameters.AddWithValue("$to", ToDatabaseDate(to));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var state = ReadDailyUsageState(reader);
            results.Add(state.ToRecord());
        }

        return results;
    }

    public async Task UpsertWorkSessionAsync(
        DateOnly workDate,
        WorkSessionRecord session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var dailyUsage = await ReadDailyUsageStateAsync(connection, transaction, workDate, cancellationToken)
            ?? throw new InvalidOperationException("No tracked daily record exists for the selected date.");
        var sessions = dailyUsage.WorkSessions?.ToList() ?? new List<WorkSessionRecord>();
        var existingIndex = sessions.FindIndex(item => string.Equals(item.Id, session.Id, StringComparison.Ordinal));

        ValidateWorkSession(workDate, session, sessions.Where((_, index) => index != existingIndex));
        if (existingIndex >= 0)
        {
            sessions[existingIndex] = session;
        }
        else
        {
            sessions.Add(session);
        }

        dailyUsage.ReplaceWorkSessions(sessions);
        await UpsertDailyUsageAsync(connection, transaction, dailyUsage, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteWorkSessionAsync(
        DateOnly workDate,
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var dailyUsage = await ReadDailyUsageStateAsync(connection, transaction, workDate, cancellationToken)
            ?? throw new InvalidOperationException("No tracked daily record exists for the selected date.");
        var sessions = dailyUsage.WorkSessions?.ToList() ?? new List<WorkSessionRecord>();
        var removed = sessions.RemoveAll(item => string.Equals(item.Id, sessionId, StringComparison.Ordinal));
        if (removed == 0)
        {
            throw new InvalidOperationException("The selected work session no longer exists.");
        }

        dailyUsage.ReplaceWorkSessions(sessions);
        await UpsertDailyUsageAsync(connection, transaction, dailyUsage, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ConfirmWorkSessionsAsync(
        DateOnly workDate,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var dailyUsage = await ReadDailyUsageStateAsync(connection, transaction, workDate, cancellationToken)
            ?? throw new InvalidOperationException("No tracked daily record exists for the selected date.");
        var sessions = dailyUsage.WorkSessions?.ToList() ?? new List<WorkSessionRecord>();
        if (sessions.Count == 0)
        {
            throw new InvalidOperationException("There are no work sessions to confirm for the selected date.");
        }

        dailyUsage.ReplaceWorkSessions(sessions.Select(session => session with
        {
            Status = WorkSessionStatus.Confirmed
        }));
        await UpsertDailyUsageAsync(connection, transaction, dailyUsage, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReplaceWorkSessionsAsync(
        DateOnly workDate,
        IReadOnlyCollection<string> sessionIds,
        IReadOnlyList<WorkSessionRecord> replacements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionIds);
        ArgumentNullException.ThrowIfNull(replacements);
        var ids = sessionIds.ToHashSet(StringComparer.Ordinal);
        if (ids.Count == 0 || ids.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one valid work-session identifier is required.", nameof(sessionIds));
        }

        if (replacements.Select(session => session.Id).Distinct(StringComparer.Ordinal).Count() != replacements.Count)
        {
            throw new ArgumentException("Replacement work sessions must have unique identifiers.", nameof(replacements));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        var dailyUsage = await ReadDailyUsageStateAsync(connection, transaction, workDate, cancellationToken)
            ?? throw new InvalidOperationException("No tracked daily record exists for the selected date.");
        var current = dailyUsage.WorkSessions?.ToList() ?? new List<WorkSessionRecord>();
        if (!ids.All(id => current.Any(session => string.Equals(session.Id, id, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException("One or more selected work sessions no longer exist.");
        }

        var updated = current.Where(session => !ids.Contains(session.Id)).ToList();
        foreach (var replacement in replacements.OrderBy(session => session.StartTimeUtc))
        {
            ValidateWorkSession(workDate, replacement, updated);
            updated.Add(replacement);
        }

        dailyUsage.ReplaceWorkSessions(updated);
        await UpsertDailyUsageAsync(connection, transaction, dailyUsage, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NetworkLocation>> GetNetworkLocationsAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<NetworkLocation>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT NetworkId, NetworkName, LocationType, LastSeenUtc, UpdatedAtUtc
            FROM Networks
            ORDER BY LastSeenUtc DESC;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadNetworkLocation(reader));
        }

        return results;
    }

    public async Task<NetworkLocation?> GetLatestNetworkAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT NetworkId, NetworkName, LocationType, LastSeenUtc, UpdatedAtUtc
            FROM Networks
            ORDER BY LastSeenUtc DESC
            LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadNetworkLocation(reader) : null;
    }

    public async Task SetNetworkLocationAsync(
        string networkId,
        LocationType locationType,
        CancellationToken cancellationToken = default)
    {
        ValidateNetworkId(networkId);

        if (!Enum.IsDefined(locationType))
        {
            throw new ArgumentOutOfRangeException(nameof(locationType));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Networks
            SET LocationType = $locationType, UpdatedAtUtc = $updatedAtUtc
            WHERE NetworkId = $networkId;
            """;
        command.Parameters.AddWithValue("$locationType", (int)locationType);
        command.Parameters.AddWithValue("$updatedAtUtc", ToDatabaseDate(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$networkId", networkId);

        var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
        if (affectedRows == 0)
        {
            throw new InvalidOperationException("The selected network has not been observed yet.");
        }
    }

    public async Task<int> GetNetworkUsageDayCountAsync(
        string networkId,
        CancellationToken cancellationToken = default)
    {
        ValidateNetworkId(networkId);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(DISTINCT daily.WorkDate)
            FROM DailyUsage AS daily,
                 json_each(daily.ActiveSecondsByNetworkJson) AS network
            WHERE network.key = $networkId
              AND CAST(network.value AS INTEGER) > 0;
            """;
        command.Parameters.AddWithValue("$networkId", networkId);

        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);
    }

    public async Task DeleteNetworkAsync(
        string networkId,
        CancellationToken cancellationToken = default)
    {
        ValidateNetworkId(networkId);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Networks WHERE NetworkId = $networkId;";
        command.Parameters.AddWithValue("$networkId", networkId);

        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            throw new InvalidOperationException("The selected network no longer exists.");
        }
    }

    public async Task AddManualAdjustmentAsync(
        DateOnly workDate,
        LocationType locationType,
        int minutes,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (locationType is LocationType.Unknown || !Enum.IsDefined(locationType))
        {
            throw new ArgumentOutOfRangeException(nameof(locationType), "Choose Home, Office or Other.");
        }

        if (minutes is 0 or < -1440 or > 1440)
        {
            throw new ArgumentOutOfRangeException(nameof(minutes), "Minutes must be between -1440 and 1440, excluding zero.");
        }

        var trimmedReason = reason?.Trim();
        if (string.IsNullOrEmpty(trimmedReason) || trimmedReason.Length > 500)
        {
            throw new ArgumentException("A reason of 1 to 500 characters is required.", nameof(reason));
        }

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ManualAdjustments
                (WorkDate, LocationType, Minutes, Reason, CreatedAtUtc)
            VALUES
                ($workDate, $locationType, $minutes, $reason, $createdAtUtc);
            """;
        command.Parameters.AddWithValue("$workDate", workDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$locationType", (int)locationType);
        command.Parameters.AddWithValue("$minutes", minutes);
        command.Parameters.AddWithValue("$reason", trimmedReason);
        command.Parameters.AddWithValue("$createdAtUtc", ToDatabaseDate(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ManualAdjustment>> GetManualAdjustmentsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (to < from)
        {
            throw new ArgumentException("The adjustment end date must not be earlier than its start.", nameof(to));
        }

        var results = new List<ManualAdjustment>();
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, WorkDate, LocationType, Minutes, Reason, CreatedAtUtc
            FROM ManualAdjustments
            WHERE WorkDate BETWEEN $from AND $to
            ORDER BY WorkDate, Id;
            """;
        command.Parameters.AddWithValue("$from", from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$to", to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new ManualAdjustment(
                reader.GetInt64(0),
                DateOnly.ParseExact(reader.GetString(1), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                (LocationType)reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetString(4),
                ParseDatabaseDate(reader.GetString(5))));
        }

        return results;
    }

    private static async Task MigrateWorkSessionsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(connection, "DailyUsage", "WorkSessionsJson", cancellationToken))
        {
            return;
        }

        await CreateMigrationBackupAsync(connection, "pre-v1.4.0", cancellationToken);
        await using var transaction = connection.BeginTransaction(deferred: false);
        await using (var alterCommand = connection.CreateCommand())
        {
            alterCommand.Transaction = transaction;
            alterCommand.CommandText = "ALTER TABLE DailyUsage ADD COLUMN WorkSessionsJson TEXT NULL;";
            await alterCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        var dailyUsage = new List<DailyUsageState>();
        await using (var readCommand = connection.CreateCommand())
        {
            readCommand.Transaction = transaction;
            readCommand.CommandText = """
                SELECT WorkDate, StartTimeUtc, EndTimeUtc, ActiveSecondsByNetworkJson,
                       IdleSeconds, MachineName, LastSampleEndUtc, WorkSessionsJson
                FROM DailyUsage;
                """;
            await using var reader = await readCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                dailyUsage.Add(ReadDailyUsageState(reader));
            }
        }

        foreach (var state in dailyUsage)
        {
            state.InitialiseImportedSession();
            await UpsertDailyUsageAsync(connection, transaction, state, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task MigrateHolidayDetailsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await ColumnExistsAsync(connection, "PublicHolidays", "Type", cancellationToken))
        {
            await using var typeCommand = connection.CreateCommand();
            typeCommand.CommandText = """
                ALTER TABLE PublicHolidays
                ADD COLUMN Type INTEGER NOT NULL DEFAULT 0 CHECK (Type BETWEEN 0 AND 2);
                """;
            await typeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!await ColumnExistsAsync(connection, "PublicHolidays", "LeaveMinutes", cancellationToken))
        {
            await using var minutesCommand = connection.CreateCommand();
            minutesCommand.CommandText = """
                ALTER TABLE PublicHolidays
                ADD COLUMN LeaveMinutes INTEGER NOT NULL DEFAULT 0 CHECK (LeaveMinutes BETWEEN 0 AND 1440);
                """;
            await minutesCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<bool> MigrateLegacyUsageSamplesAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, transaction: null, "UsageSamples", cancellationToken))
        {
            return false;
        }

        await CreateMigrationBackupAsync(connection, "pre-v1.3.0", cancellationToken);

        await using var transaction = connection.BeginTransaction(deferred: false);
        if (!await TableExistsAsync(connection, transaction, "UsageSamples", cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return false;
        }

        var dailyUsage = new Dictionary<DateOnly, DailyUsageState>();
        await using (var existingCommand = connection.CreateCommand())
        {
            existingCommand.Transaction = transaction;
            existingCommand.CommandText = """
                SELECT WorkDate, StartTimeUtc, EndTimeUtc, ActiveSecondsByNetworkJson,
                       IdleSeconds, MachineName, LastSampleEndUtc, WorkSessionsJson
                FROM DailyUsage;
                """;

            await using var reader = await existingCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var state = ReadDailyUsageState(reader);
                dailyUsage[state.WorkDate] = state;
            }
        }

        await using (var legacyCommand = connection.CreateCommand())
        {
            legacyCommand.Transaction = transaction;
            legacyCommand.CommandText = """
                SELECT Id, PeriodStartUtc, PeriodEndUtc, ActiveSeconds, IdleSeconds,
                       NetworkId, NetworkName, MachineName
                FROM UsageSamples
                ORDER BY PeriodStartUtc;
                """;

            await using var reader = await legacyCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var sample = new UsageSample(
                    reader.GetInt64(0),
                    ParseDatabaseDate(reader.GetString(1)),
                    ParseDatabaseDate(reader.GetString(2)),
                    reader.GetInt32(3),
                    reader.GetInt32(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7));
                var workDate = GetLocalWorkDate(sample.PeriodStartUtc);
                if (!dailyUsage.TryGetValue(workDate, out var state))
                {
                    state = new DailyUsageState(workDate, sample.MachineName);
                    dailyUsage.Add(workDate, state);
                }

                if (state.LastSampleEndUtc is null || sample.PeriodEndUtc > state.LastSampleEndUtc)
                {
                    state.AddSample(sample);
                }
            }
        }

        foreach (var state in dailyUsage.Values)
        {
            await UpsertDailyUsageAsync(connection, transaction, state, cancellationToken);
        }

        await using (var dropCommand = connection.CreateCommand())
        {
            dropCommand.Transaction = transaction;
            dropCommand.CommandText = "DROP TABLE UsageSamples;";
            await dropCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static async Task CreateMigrationBackupAsync(
        SqliteConnection sourceConnection,
        string versionMarker,
        CancellationToken cancellationToken)
    {
        var dataDirectory = Path.GetDirectoryName(sourceConnection.DataSource)
            ?? throw new InvalidOperationException("The database directory could not be resolved.");
        var backupPath = Path.Combine(
            dataDirectory,
            $"work-location-tracker.{versionMarker}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = backupPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString();

        await using var backupConnection = new SqliteConnection(connectionString);
        await backupConnection.OpenAsync(cancellationToken);
        sourceConnection.BackupDatabase(backupConnection);
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name = $columnName;";
        command.Parameters.AddWithValue("$columnName", columnName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<bool> TableExistsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string tableName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name);";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture) == 1;
    }

    private static async Task<DailyUsageState?> ReadDailyUsageStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DateOnly workDate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT WorkDate, StartTimeUtc, EndTimeUtc, ActiveSecondsByNetworkJson,
                   IdleSeconds, MachineName, LastSampleEndUtc, WorkSessionsJson
            FROM DailyUsage
            WHERE WorkDate = $workDate;
            """;
        command.Parameters.AddWithValue("$workDate", ToDatabaseDate(workDate));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadDailyUsageState(reader) : null;
    }

    private static DailyUsageState ReadDailyUsageState(SqliteDataReader reader) => new(
        DateOnly.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
        reader.GetString(5),
        reader.IsDBNull(1) ? null : ParseDatabaseDate(reader.GetString(1)),
        reader.IsDBNull(2) ? null : ParseDatabaseDate(reader.GetString(2)),
        DeserializeNetworkSeconds(reader.GetString(3)),
        reader.GetInt32(4),
        ParseDatabaseDate(reader.GetString(6)),
        reader.IsDBNull(7) ? null : DeserializeWorkSessions(reader.GetString(7)));

    private static async Task UpsertDailyUsageAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        DailyUsageState state,
        CancellationToken cancellationToken)
    {
        if (state.LastSampleEndUtc is null)
        {
            throw new InvalidOperationException("A daily usage record must include its latest sample time.");
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO DailyUsage
                (WorkDate, StartTimeUtc, EndTimeUtc, ActiveSecondsByNetworkJson,
                 IdleSeconds, MachineName, LastSampleEndUtc, UpdatedAtUtc, WorkSessionsJson)
            VALUES
                ($workDate, $startTimeUtc, $endTimeUtc, $networkSeconds,
                 $idleSeconds, $machineName, $lastSampleEndUtc, $updatedAtUtc, $workSessions)
            ON CONFLICT (WorkDate) DO UPDATE SET
                StartTimeUtc = excluded.StartTimeUtc,
                EndTimeUtc = excluded.EndTimeUtc,
                ActiveSecondsByNetworkJson = excluded.ActiveSecondsByNetworkJson,
                IdleSeconds = excluded.IdleSeconds,
                MachineName = excluded.MachineName,
                LastSampleEndUtc = excluded.LastSampleEndUtc,
                UpdatedAtUtc = excluded.UpdatedAtUtc,
                WorkSessionsJson = excluded.WorkSessionsJson;
            """;
        command.Parameters.AddWithValue("$workDate", ToDatabaseDate(state.WorkDate));
        command.Parameters.AddWithValue("$startTimeUtc", ToDatabaseValue(state.StartTimeUtc));
        command.Parameters.AddWithValue("$endTimeUtc", ToDatabaseValue(state.EndTimeUtc));
        command.Parameters.AddWithValue("$networkSeconds", JsonSerializer.Serialize(state.ActiveSecondsByNetwork));
        command.Parameters.AddWithValue("$idleSeconds", state.IdleSeconds);
        command.Parameters.AddWithValue("$machineName", state.MachineName);
        command.Parameters.AddWithValue("$lastSampleEndUtc", ToDatabaseDate(state.LastSampleEndUtc.Value));
        command.Parameters.AddWithValue("$updatedAtUtc", ToDatabaseDate(DateTimeOffset.UtcNow));
        command.Parameters.AddWithValue("$workSessions", SerializeWorkSessions(state.WorkSessions));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static SortedDictionary<string, int> DeserializeNetworkSeconds(string json)
    {
        var values = JsonSerializer.Deserialize<Dictionary<string, int>>(json)
            ?? new Dictionary<string, int>(StringComparer.Ordinal);
        if (values.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0))
        {
            throw new InvalidDataException("The daily network usage data is invalid.");
        }

        return new SortedDictionary<string, int>(values, StringComparer.Ordinal);
    }

    private static object SerializeWorkSessions(IReadOnlyList<WorkSessionRecord>? sessions) =>
        sessions is null ? DBNull.Value : JsonSerializer.Serialize(sessions);

    private static List<WorkSessionRecord> DeserializeWorkSessions(string json)
    {
        var sessions = JsonSerializer.Deserialize<List<WorkSessionRecord>>(json)
            ?? new List<WorkSessionRecord>();
        if (sessions.Count > 100 ||
            sessions.Any(session =>
                session is null ||
                string.IsNullOrWhiteSpace(session.Id) ||
                session.EndTimeUtc <= session.StartTimeUtc ||
                session.BreakMinutes < 0 ||
                session.ActiveSecondsByNetwork is null ||
                session.ActiveSecondsByNetwork.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0)))
        {
            throw new InvalidDataException("The daily work-session data is invalid.");
        }

        return sessions;
    }

    private static IReadOnlyList<UsageSample> SplitByLocalDate(UsageSample sample)
    {
        var boundaries = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var currentStart = sample.PeriodStartUtc;

        while (GetLocalWorkDate(currentStart) != GetLocalWorkDate(sample.PeriodEndUtc.AddTicks(-1)))
        {
            var nextDate = GetLocalWorkDate(currentStart).AddDays(1);
            var localMidnight = DateTime.SpecifyKind(nextDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
            var boundary = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, TimeZoneInfo.Local));
            boundaries.Add((currentStart, boundary));
            currentStart = boundary;
        }

        boundaries.Add((currentStart, sample.PeriodEndUtc));
        if (boundaries.Count == 1)
        {
            return new[] { sample };
        }

        var totalSeconds = (sample.PeriodEndUtc - sample.PeriodStartUtc).TotalSeconds;
        var activeRemaining = sample.ActiveSeconds;
        var idleRemaining = sample.IdleSeconds;
        var results = new List<UsageSample>(boundaries.Count);

        for (var index = 0; index < boundaries.Count; index++)
        {
            var boundary = boundaries[index];
            var isLast = index == boundaries.Count - 1;
            var proportion = (boundary.End - boundary.Start).TotalSeconds / totalSeconds;
            var activeSeconds = isLast
                ? activeRemaining
                : Math.Min(activeRemaining, (int)Math.Floor(sample.ActiveSeconds * proportion));
            var idleSeconds = isLast
                ? idleRemaining
                : Math.Min(idleRemaining, (int)Math.Floor(sample.IdleSeconds * proportion));
            activeRemaining -= activeSeconds;
            idleRemaining -= idleSeconds;
            results.Add(sample with
            {
                PeriodStartUtc = boundary.Start,
                PeriodEndUtc = boundary.End,
                ActiveSeconds = activeSeconds,
                IdleSeconds = idleSeconds
            });
        }

        return results;
    }

    private static void ValidateNetworkId(string networkId)
    {
        if (string.IsNullOrWhiteSpace(networkId))
        {
            throw new ArgumentException("A network identifier is required.", nameof(networkId));
        }
    }

    private static void ValidateWorkScheduleSettings(WorkScheduleSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.DailyTargetMinutes is < 1 or > 1440)
        {
            throw new ArgumentOutOfRangeException(
                nameof(settings),
                "The daily target must be between 1 minute and 24 hours.");
        }

        if (settings.ScheduledDays is WorkingDays.None ||
            (settings.ScheduledDays & ~WorkingDays.All) != 0)
        {
            throw new ArgumentException("Choose at least one valid standard workday.", nameof(settings));
        }
    }

    private static void ValidateWorkSession(
        DateOnly workDate,
        WorkSessionRecord session,
        IEnumerable<WorkSessionRecord> otherSessions)
    {
        if (string.IsNullOrWhiteSpace(session.Id) || session.Id.Length > 64)
        {
            throw new ArgumentException("The work session identifier is invalid.", nameof(session));
        }

        if (session.EndTimeUtc <= session.StartTimeUtc ||
            GetLocalWorkDate(session.StartTimeUtc) != workDate ||
            GetLocalWorkDate(session.EndTimeUtc.AddTicks(-1)) != workDate)
        {
            throw new ArgumentException("A work session must start and finish within the selected calendar day.", nameof(session));
        }

        var durationMinutes = (session.EndTimeUtc - session.StartTimeUtc).TotalMinutes;
        if (session.BreakMinutes < 0 || session.BreakMinutes >= durationMinutes)
        {
            throw new ArgumentException("Break time must be shorter than the work-session duration.", nameof(session));
        }

        if (!Enum.IsDefined(session.Status) ||
            !Enum.IsDefined(session.Source) ||
            session.LocationOverride is { } location && !Enum.IsDefined(location))
        {
            throw new ArgumentException("The work-session classification is invalid.", nameof(session));
        }

        if (session.ActiveSecondsByNetwork is null ||
            session.ActiveSecondsByNetwork.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 0))
        {
            throw new ArgumentException("The work-session network data is invalid.", nameof(session));
        }

        if ((session.Note ?? string.Empty).Length > 200)
        {
            throw new ArgumentException("A work-session note cannot exceed 200 characters.", nameof(session));
        }

        var existing = otherSessions.ToArray();
        if (existing.Length >= 100)
        {
            throw new InvalidOperationException("A daily record cannot contain more than 100 work sessions.");
        }

        if (existing.Any(other => session.StartTimeUtc < other.EndTimeUtc && session.EndTimeUtc > other.StartTimeUtc))
        {
            throw new InvalidOperationException("Work sessions cannot overlap. Edit or delete the existing session first.");
        }
    }

    private static async Task VacuumDatabaseAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await CheckpointDatabaseAsync(connection, cancellationToken);

        await using var vacuumCommand = connection.CreateCommand();
        vacuumCommand.CommandText = "VACUUM;";
        await vacuumCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task CheckpointDatabaseAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 10
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 10000;
            PRAGMA journal_size_limit = 4194304;
            PRAGMA wal_autocheckpoint = 250;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);

        return connection;
    }

    private static NetworkLocation ReadNetworkLocation(SqliteDataReader reader)
    {
        return new NetworkLocation(
            reader.GetString(0),
            reader.GetString(1),
            (LocationType)reader.GetInt32(2),
            ParseDatabaseDate(reader.GetString(3)),
            reader.IsDBNull(4) ? null : ParseDatabaseDate(reader.GetString(4)));
    }

    private static void ValidateSample(UsageSample sample)
    {
        if (sample.PeriodEndUtc <= sample.PeriodStartUtc)
        {
            throw new ArgumentException("A sample must end after it starts.", nameof(sample));
        }

        if (sample.ActiveSeconds < 0 || sample.IdleSeconds < 0)
        {
            throw new ArgumentException("Sample durations cannot be negative.", nameof(sample));
        }

        var measuredSeconds = sample.ActiveSeconds + sample.IdleSeconds;
        var periodSeconds = (int)Math.Ceiling((sample.PeriodEndUtc - sample.PeriodStartUtc).TotalSeconds);
        if (measuredSeconds > periodSeconds + 1)
        {
            throw new ArgumentException("Measured time cannot exceed the sample period.", nameof(sample));
        }

        if (string.IsNullOrWhiteSpace(sample.NetworkId) ||
            string.IsNullOrWhiteSpace(sample.NetworkName) ||
            string.IsNullOrWhiteSpace(sample.MachineName))
        {
            throw new ArgumentException("Network and machine values are required.", nameof(sample));
        }
    }

    private static string ValidatePublicHoliday(PublicHoliday publicHoliday)
    {
        ArgumentNullException.ThrowIfNull(publicHoliday);
        var name = (publicHoliday.Name ?? string.Empty).Trim();
        if (name.Length > 120)
        {
            throw new ArgumentException("The Holiday description cannot exceed 120 characters.", nameof(publicHoliday));
        }

        if (!Enum.IsDefined(publicHoliday.Type))
        {
            throw new ArgumentException("Choose a valid Holiday type.", nameof(publicHoliday));
        }

        if (publicHoliday.Type == HolidayType.PublicHoliday && publicHoliday.LeaveMinutes != 0)
        {
            throw new ArgumentException("A Public Holiday is always a full day and cannot have leave minutes.", nameof(publicHoliday));
        }

        if (publicHoliday.Type != HolidayType.PublicHoliday && publicHoliday.LeaveMinutes is < 1 or > 1440)
        {
            throw new ArgumentException("Annual Leave and Sick Leave must be between 1 minute and 24 hours.", nameof(publicHoliday));
        }

        return name;
    }

    private static string ToDatabaseDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    private static string ToDatabaseDate(DateOnly value) =>
        value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static object ToDatabaseValue(DateTimeOffset? value) =>
        value is null ? DBNull.Value : ToDatabaseDate(value.Value);

    private static DateTimeOffset ParseDatabaseDate(string value) =>
        DateTimeOffset.ParseExact(value, DateTimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static DateOnly GetLocalWorkDate(DateTimeOffset value) =>
        DateOnly.FromDateTime(value.ToLocalTime().DateTime);

    private sealed class DailyUsageState
    {
        public DailyUsageState(
            DateOnly workDate,
            string machineName,
            DateTimeOffset? startTimeUtc = null,
            DateTimeOffset? endTimeUtc = null,
            SortedDictionary<string, int>? activeSecondsByNetwork = null,
            int idleSeconds = 0,
            DateTimeOffset? lastSampleEndUtc = null,
            List<WorkSessionRecord>? workSessions = null)
        {
            WorkDate = workDate;
            MachineName = machineName;
            StartTimeUtc = startTimeUtc;
            EndTimeUtc = endTimeUtc;
            ActiveSecondsByNetwork = activeSecondsByNetwork ?? new SortedDictionary<string, int>(StringComparer.Ordinal);
            IdleSeconds = idleSeconds;
            LastSampleEndUtc = lastSampleEndUtc;
            WorkSessions = workSessions;
        }

        public DateOnly WorkDate { get; }

        public DateTimeOffset? StartTimeUtc { get; private set; }

        public DateTimeOffset? EndTimeUtc { get; private set; }

        public SortedDictionary<string, int> ActiveSecondsByNetwork { get; }

        public int IdleSeconds { get; private set; }

        public string MachineName { get; private set; }

        public DateTimeOffset? LastSampleEndUtc { get; private set; }

        public List<WorkSessionRecord>? WorkSessions { get; private set; }

        public void AddSample(UsageSample sample)
        {
            MachineName = sample.MachineName;
            IdleSeconds = checked(IdleSeconds + sample.IdleSeconds);
            LastSampleEndUtc = LastSampleEndUtc is null || sample.PeriodEndUtc > LastSampleEndUtc
                ? sample.PeriodEndUtc
                : LastSampleEndUtc;

            if (sample.ActiveSeconds == 0)
            {
                return;
            }

            ActiveSecondsByNetwork[sample.NetworkId] = checked(
                ActiveSecondsByNetwork.GetValueOrDefault(sample.NetworkId) + sample.ActiveSeconds);
            StartTimeUtc = StartTimeUtc is null || sample.PeriodStartUtc < StartTimeUtc
                ? sample.PeriodStartUtc
                : StartTimeUtc;
            EndTimeUtc = EndTimeUtc is null || sample.PeriodEndUtc > EndTimeUtc
                ? sample.PeriodEndUtc
                : EndTimeUtc;

            WorkSessions ??= new List<WorkSessionRecord>();
            if (WorkSessions.Any(session =>
                    session.Status == WorkSessionStatus.Confirmed &&
                    sample.PeriodStartUtc < session.EndTimeUtc &&
                    sample.PeriodEndUtc > session.StartTimeUtc))
            {
                return;
            }

            var latestSession = WorkSessions
                .Where(session => session.Status == WorkSessionStatus.Suggested)
                .OrderByDescending(session => session.EndTimeUtc)
                .FirstOrDefault();
            if (latestSession is not null && sample.PeriodStartUtc - latestSession.EndTimeUtc <= SuggestedSessionGap)
            {
                var networkSeconds = new SortedDictionary<string, int>(
                    latestSession.ActiveSecondsByNetwork.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                    StringComparer.Ordinal)
                {
                    [sample.NetworkId] = checked(
                        latestSession.ActiveSecondsByNetwork.GetValueOrDefault(sample.NetworkId) + sample.ActiveSeconds)
                };
                var sessionEnd = sample.PeriodEndUtc > latestSession.EndTimeUtc
                    ? sample.PeriodEndUtc
                    : latestSession.EndTimeUtc;
                var trackedSeconds = networkSeconds.Values.Sum();
                var inferredBreakMinutes = Math.Max(
                    0,
                    (int)Math.Round(((sessionEnd - latestSession.StartTimeUtc).TotalSeconds - trackedSeconds) / 60d));
                var merged = latestSession with
                {
                    EndTimeUtc = sessionEnd,
                    BreakMinutes = inferredBreakMinutes,
                    ActiveSecondsByNetwork = networkSeconds
                };
                WorkSessions[WorkSessions.IndexOf(latestSession)] = merged;
            }
            else
            {
                WorkSessions.Add(new WorkSessionRecord(
                    Guid.NewGuid().ToString("N"),
                    sample.PeriodStartUtc,
                    sample.PeriodEndUtc,
                    0,
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [sample.NetworkId] = sample.ActiveSeconds
                    },
                    null,
                    WorkSessionStatus.Suggested,
                    WorkSessionSource.Automatic,
                    string.Empty));
            }
        }

        public void InitialiseImportedSession()
        {
            if (WorkSessions is not null)
            {
                return;
            }

            WorkSessions = new List<WorkSessionRecord>();
            var trackedSeconds = ActiveSecondsByNetwork.Values.Sum();
            if (StartTimeUtc is null || EndTimeUtc is null || trackedSeconds == 0)
            {
                return;
            }

            var inferredBreakMinutes = Math.Max(
                0,
                (int)Math.Round(((EndTimeUtc.Value - StartTimeUtc.Value).TotalSeconds - trackedSeconds) / 60d));
            WorkSessions.Add(new WorkSessionRecord(
                Guid.NewGuid().ToString("N"),
                StartTimeUtc.Value,
                EndTimeUtc.Value,
                inferredBreakMinutes,
                new Dictionary<string, int>(ActiveSecondsByNetwork, StringComparer.Ordinal),
                null,
                WorkSessionStatus.Suggested,
                WorkSessionSource.ImportedSummary,
                "Imported from the earlier daily summary; review the start, finish and break."));
        }

        public void ReplaceWorkSessions(IEnumerable<WorkSessionRecord> sessions)
        {
            WorkSessions = sessions
                .OrderBy(session => session.StartTimeUtc)
                .ToList();
        }

        public DailyUsageRecord ToRecord() => new(
            WorkDate,
            StartTimeUtc,
            EndTimeUtc,
            new Dictionary<string, int>(ActiveSecondsByNetwork, StringComparer.Ordinal),
            IdleSeconds,
            MachineName,
            WorkSessions?.ToArray());
    }
}
