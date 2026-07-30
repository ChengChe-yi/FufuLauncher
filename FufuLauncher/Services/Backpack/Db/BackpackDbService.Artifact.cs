/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using FufuLauncher.Models.Backpack;
using Microsoft.Data.Sqlite;

namespace FufuLauncher.Services.Backpack;

public sealed partial class BackpackDbService
{
    public IReadOnlyList<ArtifactEntry> LoadArtifacts()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "SELECT guid,id,set_name,name,slot,locked,level,rank,main_stat_type,main_stat_raw,sub_stats FROM artifacts ORDER BY locked DESC,rank DESC,level DESC";
        using var r = cmd.ExecuteReader();
        var list = new List<ArtifactEntry>();
        while (r.Read())
        {
            var subStats = JsonSerializer.Deserialize<ArtifactSubStat[]>(r.GetString(10)) ?? [];
            list.Add(new ArtifactEntry(
                (uint)r.GetInt64(1),
                r.GetString(0),
                r.GetString(2),
                r.GetString(3),
                r.GetString(4),
                r.GetInt32(5) != 0,
                r.GetInt32(6),
                r.GetInt32(7),
                new ArtifactMainStat(r.GetString(8), r.GetString(9)),
                subStats));
        }
        return list;
    }

    public void SaveArtifacts(IEnumerable<ArtifactEntry> artifacts)
    {
        using var tx  = _db.BeginTransaction();
        using var del = _db.CreateCommand();
        del.CommandText = "DELETE FROM artifacts";
        del.ExecuteNonQuery();

        using var ins = _db.CreateCommand();
        ins.CommandText =
            "INSERT INTO artifacts " +
            "(guid,id,set_name,name,slot,locked,level,rank,main_stat_type,main_stat_raw,sub_stats) " +
            "VALUES ($g,$i,$sn,$n,$sl,$lk,$l,$r,$mt,$mr,$ss)";
        var pg  = ins.Parameters.Add("$g",  SqliteType.Text);
        var pi  = ins.Parameters.Add("$i",  SqliteType.Integer);
        var psn = ins.Parameters.Add("$sn", SqliteType.Text);
        var pn  = ins.Parameters.Add("$n",  SqliteType.Text);
        var psl = ins.Parameters.Add("$sl", SqliteType.Text);
        var plk = ins.Parameters.Add("$lk", SqliteType.Integer);
        var pl  = ins.Parameters.Add("$l",  SqliteType.Integer);
        var pr  = ins.Parameters.Add("$r",  SqliteType.Integer);
        var pmt = ins.Parameters.Add("$mt", SqliteType.Text);
        var pmr = ins.Parameters.Add("$mr", SqliteType.Text);
        var pss = ins.Parameters.Add("$ss", SqliteType.Text);

        foreach (var a in artifacts)
        {
            pg.Value  = a.Guid;
            pi.Value  = (long)a.Id;
            psn.Value = a.SetName;
            pn.Value  = a.Name;
            psl.Value = a.Slot;
            plk.Value = a.Locked ? 1 : 0;
            pl.Value  = a.Level;
            pr.Value  = a.Rank;
            pmt.Value = a.MainStat.Type;
            pmr.Value = a.MainStat.TypeRaw;
            pss.Value = JsonSerializer.Serialize(a.SubStats);
            ins.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
