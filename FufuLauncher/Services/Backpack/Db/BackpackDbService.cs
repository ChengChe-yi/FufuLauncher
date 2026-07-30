/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using Microsoft.Data.Sqlite;

namespace FufuLauncher.Services.Backpack;

public sealed partial class BackpackDbService : IDisposable
{
    private readonly SqliteConnection _db;

    public BackpackDbService()
    {
        var dir = Path.Combine(AppPaths.DataDir, "Backpack");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "backpack.db");
        _db = new SqliteConnection($"Data Source={path}");
        _db.Open();
        InitSchema();
    }

    private void InitSchema()
    {
        Exec("""
            CREATE TABLE IF NOT EXISTS weapons (
                guid        TEXT    PRIMARY KEY,
                id          INTEGER NOT NULL,
                name        TEXT    NOT NULL,
                type        TEXT    NOT NULL,
                rank        INTEGER NOT NULL,
                special_prop TEXT   NOT NULL,
                level       INTEGER NOT NULL,
                promote     INTEGER NOT NULL,
                refine      INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS artifacts (
                guid             TEXT    PRIMARY KEY,
                id               INTEGER NOT NULL,
                set_name         TEXT    NOT NULL,
                name             TEXT    NOT NULL,
                slot             TEXT    NOT NULL,
                locked           INTEGER NOT NULL,
                level            INTEGER NOT NULL,
                rank             INTEGER NOT NULL,
                main_stat_type   TEXT    NOT NULL,
                main_stat_raw    TEXT    NOT NULL,
                sub_stats        TEXT    NOT NULL
            );
            CREATE TABLE IF NOT EXISTS materials (
                id    INTEGER PRIMARY KEY,
                count INTEGER NOT NULL
            );
            CREATE TABLE IF NOT EXISTS props (
                id    INTEGER PRIMARY KEY,
                value INTEGER NOT NULL
            );
            """);
        try { Exec("ALTER TABLE artifacts RENAME COLUMN equipped TO locked"); } catch { }
    }

    private void Exec(string sql)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _db.Dispose();
}
