using FufuLauncher.Models.Backpack;
using Microsoft.Data.Sqlite;

namespace FufuLauncher.Services.Backpack;

public sealed partial class BackpackDbService
{
    public IReadOnlyList<WeaponEntry> LoadWeapons()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText =
            "SELECT guid,id,name,type,rank,special_prop,level,promote,refine FROM weapons ORDER BY rank DESC,id";
        using var r = cmd.ExecuteReader();
        var list = new List<WeaponEntry>();
        while (r.Read())
            list.Add(new WeaponEntry(
                (uint)r.GetInt64(1),
                r.GetString(0),
                r.GetString(2),
                r.GetString(3),
                r.GetInt32(4),
                r.GetString(5),
                r.GetInt32(6),
                r.GetInt32(7),
                r.GetInt32(8)));
        return list;
    }

    public void SaveWeapons(IEnumerable<WeaponEntry> weapons)
    {
        using var tx  = _db.BeginTransaction();
        using var del = _db.CreateCommand();
        del.CommandText = "DELETE FROM weapons";
        del.ExecuteNonQuery();

        using var ins = _db.CreateCommand();
        ins.CommandText =
            "INSERT INTO weapons (guid,id,name,type,rank,special_prop,level,promote,refine) " +
            "VALUES ($g,$i,$n,$t,$r,$sp,$l,$p,$rf)";
        var pg  = ins.Parameters.Add("$g",  SqliteType.Text);
        var pi  = ins.Parameters.Add("$i",  SqliteType.Integer);
        var pn  = ins.Parameters.Add("$n",  SqliteType.Text);
        var pt  = ins.Parameters.Add("$t",  SqliteType.Text);
        var pr  = ins.Parameters.Add("$r",  SqliteType.Integer);
        var psp = ins.Parameters.Add("$sp", SqliteType.Text);
        var pl  = ins.Parameters.Add("$l",  SqliteType.Integer);
        var pp  = ins.Parameters.Add("$p",  SqliteType.Integer);
        var prf = ins.Parameters.Add("$rf", SqliteType.Integer);

        foreach (var w in weapons)
        {
            pg.Value  = w.Guid;
            pi.Value  = (long)w.Id;
            pn.Value  = w.Name;
            pt.Value  = w.Type;
            pr.Value  = w.Rank;
            psp.Value = w.SpecialProp;
            pl.Value  = w.Level;
            pp.Value  = w.Promote;
            prf.Value = w.Refine;
            ins.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
