using System.IO;
using System.Text.Json.Serialization;
using FufuLauncher.Models.Backpack;

namespace FufuLauncher.Services.Backpack;

public sealed class WeaponMetaService
{
    private static readonly HashSet<string> PercentProps =
    [
        "FIGHT_PROP_ATTACK_PERCENT",
        "FIGHT_PROP_CHARGE_EFFICIENCY",
        "FIGHT_PROP_CRITICAL",
        "FIGHT_PROP_CRITICAL_HURT",
        "FIGHT_PROP_DEFENSE_PERCENT",
        "FIGHT_PROP_HP_PERCENT",
        "FIGHT_PROP_PHYSICAL_ADD_HURT",
    ];

    private readonly Dictionary<uint, WeaponMeta>  _meta;
    private readonly Dictionary<string, float[]>   _curves;
    private readonly Dictionary<string, float[]>   _promotes;

    public WeaponMetaService()
    {
        var dir   = Path.Combine(AppContext.BaseDirectory, "Assets", "Backpack", "Weapon");
        _meta     = JsonLoader.Load<WeaponMeta[]>(Path.Combine(dir, "weapons.json"))
                        ?.ToDictionary(e => (uint)e.Id) ?? [];
        _curves   = JsonLoader.Load<Dictionary<string, float[]>>(Path.Combine(dir, "weapon_curves.json"))   ?? [];
        _promotes = JsonLoader.Load<Dictionary<string, float[]>>(Path.Combine(dir, "weapon_promotes.json")) ?? [];
    }

    public Uri? GetIcon(uint id)
    {
        if (_meta.TryGetValue(id, out var m) && !string.IsNullOrEmpty(m.Icon))
            return StaticResources.WeaponIcon(m.Icon);
        return null;
    }

    public IReadOnlyList<WeaponEntry> GetDefaultEntries() =>
        [.. _meta.Values
            .OrderByDescending(m => m.Rank).ThenBy(m => m.Id)
            .Select(m => new WeaponEntry((uint)m.Id, string.Empty, m.Name, m.Type, m.Rank, m.SpecialProp, 0, 0, 0))];

    public (int Atk, string Sub) CalcStats(uint id, int level, int promote)
    {
        if (!_meta.TryGetValue(id, out var m)) return (0, string.Empty);

        var l = Math.Clamp(level,   1, 90) - 1;
        var p = Math.Clamp(promote, 0, 6);

        var atkMul   = _curves.TryGetValue(m.AtkCurve, out var ac) && ac.Length > l ? ac[l] : 1f;
        var atkBonus = _promotes.TryGetValue(m.PromoteId.ToString(), out var pb) && pb.Length > p ? pb[p] : 0f;
        var atk      = (int)Math.Round(m.AtkBase * atkMul + atkBonus);

        if (string.IsNullOrEmpty(m.SubCurve) || m.SubBase == 0f)
            return (atk, string.Empty);

        var subMul = _curves.TryGetValue(m.SubCurve, out var sc) && sc.Length > l ? sc[l] : 1f;
        var rawSub = m.SubBase * subMul;
        var sub    = PercentProps.Contains(m.SubProp)
            ? $"{rawSub * 100:F1}%"
            : $"{(int)Math.Round(rawSub)}";

        return (atk, sub);
    }

    public (string Name, string Desc) GetSkill(uint id, int refine)
    {
        if (!_meta.TryGetValue(id, out var m)) return (string.Empty, string.Empty);
        var refs = m.Refinements ?? [];
        if (refs.Length == 0) return (m.PassiveName ?? string.Empty, string.Empty);
        var idx  = Math.Clamp(refine - 1, 0, refs.Length - 1);
        return (m.PassiveName ?? string.Empty, refs[idx]);
    }

    public string GetFlavorText(uint id) =>
        _meta.TryGetValue(id, out var m) ? m.FlavorText ?? string.Empty : string.Empty;

    private sealed record WeaponMeta(
        [property: JsonPropertyName("id")]          int      Id,
        [property: JsonPropertyName("name")]        string   Name,
        [property: JsonPropertyName("rank")]        int      Rank,
        [property: JsonPropertyName("type")]        string   Type,
        [property: JsonPropertyName("specialProp")] string   SpecialProp,
        [property: JsonPropertyName("icon")]        string   Icon,
        [property: JsonPropertyName("atkBase")]     float    AtkBase,
        [property: JsonPropertyName("atkCurve")]    string   AtkCurve,
        [property: JsonPropertyName("subBase")]     float    SubBase,
        [property: JsonPropertyName("subCurve")]    string   SubCurve,
        [property: JsonPropertyName("subProp")]     string   SubProp,
        [property: JsonPropertyName("promoteId")]   int      PromoteId,
        [property: JsonPropertyName("passiveName")] string?  PassiveName,
        [property: JsonPropertyName("flavorText")]  string?  FlavorText,
        [property: JsonPropertyName("refinements")] string[]? Refinements
    );
}
