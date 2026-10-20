/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Data.Entities;
using FufuLauncher.Helpers;
using FufuLauncher.Models.CycleChallenge;
using Microsoft.EntityFrameworkCore;

namespace FufuLauncher.Data.Repositories;

/// <summary>
/// 周期挑战记录仓储：独立 SQLite 文件 + PRAGMA user_version 迁移（参考 Starward DatabaseService）。
/// 只做新增，不改动其它 DbContext / 数据文件。
/// </summary>
public sealed class CycleChallengeRepository
{
    private const int CurrentSchemaVersion = 1;

    private readonly string _dbPath;
    private readonly object _initLock = new();
    private bool _initialized;

    public CycleChallengeRepository()
        : this(Path.Combine(AppPaths.DataDir, "cycle_challenge.db"))
    {
    }

    public CycleChallengeRepository(string dbPath)
    {
        _dbPath = dbPath;
        var folder = Path.GetDirectoryName(_dbPath);
        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }
    }

    public string DatabasePath => _dbPath;

    private CycleChallengeDbContext CreateContext() => new(_dbPath);

    /// <summary>建库/升级（幂等）。</summary>
    public void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initLock)
        {
            if (_initialized)
            {
                return;
            }

            using var db = CreateContext();
            var connection = db.Database.GetDbConnection();
            connection.Open();

            int version;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "PRAGMA user_version;";
                version = Convert.ToInt32(command.ExecuteScalar() ?? 0);
            }

            if (version < 1)
            {
                foreach (var sql in SchemaV1)
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }

                version = 1;
            }

            connection.Close();
            _initialized = true;
        }
    }

    private static readonly string[] SchemaV1 =
    [
        """
        CREATE TABLE IF NOT EXISTS CycleChallengeRecord
        (
            Uid        TEXT    NOT NULL,
            Mode       INTEGER NOT NULL,
            ScheduleId TEXT    NOT NULL,
            AccountId  TEXT,
            StartUtc   TEXT,
            EndUtc     TEXT,
            Summary    TEXT,
            Payload    TEXT    NOT NULL,
            FetchedAt  TEXT    NOT NULL,
            ApiVersion TEXT,
            CONSTRAINT PK_CycleChallengeRecord PRIMARY KEY (Uid, Mode, ScheduleId)
        );
        """,
        "CREATE INDEX IF NOT EXISTS IX_CycleChallengeRecord_Lookup ON CycleChallengeRecord (Uid, Mode);",
        $"PRAGMA user_version = {CurrentSchemaVersion};",
    ];

    /// <summary>同键覆盖写入（D5：同期刷新最新覆盖旧的）。</summary>
    public void Upsert(CycleChallengeRecordEntity record)
    {
        EnsureInitialized();
        lock (_initLock)
        {
            using var db = CreateContext();
            var existing = db.CycleChallengeRecords.Find(record.Uid, record.Mode, record.ScheduleId);
            if (existing is not null)
            {
                db.CycleChallengeRecords.Remove(existing);
                db.SaveChanges();
            }

            db.CycleChallengeRecords.Add(record);
            db.SaveChanges();
        }
    }

    public IReadOnlyList<ChallengePeriod> GetPeriods(string uid, ChallengeMode mode)
    {
        EnsureInitialized();
        using var db = CreateContext();
        var modeValue = (int)mode;
        var rows = db.CycleChallengeRecords
            .AsNoTracking()
            .Where(x => x.Uid == uid && x.Mode == modeValue)
            .Select(x => new { x.ScheduleId, x.StartUtc, x.EndUtc, x.FetchedAt })
            .ToList();

        return rows
            .OrderByDescending(x => ParseSchedule(x.ScheduleId))
            .Select(x => new ChallengePeriod(
                uid,
                mode,
                x.ScheduleId,
                x.StartUtc,
                x.EndUtc,
                DateTimeOffset.TryParse(x.FetchedAt, out var fetched) ? fetched : DateTimeOffset.MinValue))
            .ToList();
    }

    public CycleChallengeRecordEntity? Get(string uid, ChallengeMode mode, string scheduleId)
    {
        EnsureInitialized();
        using var db = CreateContext();
        var modeValue = (int)mode;
        return db.CycleChallengeRecords
            .AsNoTracking()
            .FirstOrDefault(x => x.Uid == uid && x.Mode == modeValue && x.ScheduleId == scheduleId);
    }

    public CycleChallengeRecordEntity? GetLatest(string uid, ChallengeMode mode)
    {
        var periods = GetPeriods(uid, mode);
        if (periods.Count == 0)
        {
            return null;
        }

        return Get(uid, mode, periods[0].ScheduleId);
    }

    public int Count(string uid, ChallengeMode mode)
    {
        EnsureInitialized();
        using var db = CreateContext();
        var modeValue = (int)mode;
        return db.CycleChallengeRecords.Count(x => x.Uid == uid && x.Mode == modeValue);
    }

    /// <summary>仅保留最近 keep 期，返回删除条数。</summary>
    public int Trim(string uid, ChallengeMode mode, int keep)
    {
        var periods = GetPeriods(uid, mode);
        if (periods.Count <= keep)
        {
            return 0;
        }

        var remove = periods.Skip(keep).Select(x => x.ScheduleId).ToHashSet(StringComparer.Ordinal);
        lock (_initLock)
        {
            using var db = CreateContext();
            var modeValue = (int)mode;
            var rows = db.CycleChallengeRecords
                .Where(x => x.Uid == uid && x.Mode == modeValue && remove.Contains(x.ScheduleId))
                .ToList();
            db.CycleChallengeRecords.RemoveRange(rows);
            db.SaveChanges();
            return rows.Count;
        }
    }

    private static long ParseSchedule(string scheduleId) =>
        long.TryParse(scheduleId, out var value) ? value : long.MinValue;
}
