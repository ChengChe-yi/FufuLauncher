/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.Data.Sqlite;

namespace FufuLauncher.Services.Backpack;

public sealed partial class BackpackDbService
{
    public Dictionary<uint, ulong> LoadMaterialCounts()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id, count FROM materials";
        using var r = cmd.ExecuteReader();
        var dict = new Dictionary<uint, ulong>();
        while (r.Read())
            dict[(uint)r.GetInt64(0)] = (ulong)r.GetInt64(1);
        return dict;
    }

    public void SaveMaterials(Dictionary<uint, ulong> counts)
    {
        using var tx  = _db.BeginTransaction();
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "INSERT OR REPLACE INTO materials (id, count) VALUES ($id, $c)";
        var pid = cmd.Parameters.Add("$id", SqliteType.Integer);
        var pc  = cmd.Parameters.Add("$c",  SqliteType.Integer);

        foreach (var (id, count) in counts)
        {
            pid.Value = (long)id;
            pc.Value  = (long)count;
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
