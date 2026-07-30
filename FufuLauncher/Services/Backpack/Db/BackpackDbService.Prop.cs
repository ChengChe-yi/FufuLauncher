/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.Data.Sqlite;

namespace FufuLauncher.Services.Backpack;

public sealed partial class BackpackDbService
{
    public Dictionary<uint, long> LoadProps()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id, value FROM props";
        using var r = cmd.ExecuteReader();
        var dict = new Dictionary<uint, long>();
        while (r.Read())
            dict[(uint)r.GetInt64(0)] = r.GetInt64(1);
        return dict;
    }

    public void SaveProps(Dictionary<uint, long> props)
    {
        using var tx  = _db.BeginTransaction();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO props (id, value) VALUES ($id, $v)";
        var pid = cmd.Parameters.Add("$id", SqliteType.Integer);
        var pv  = cmd.Parameters.Add("$v",  SqliteType.Integer);

        foreach (var (id, value) in props)
        {
            pid.Value = (long)id;
            pv.Value  = value;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
