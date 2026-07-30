/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;
using FufuLauncher.Helpers;
using FufuLauncher.Models.Backpack;

namespace FufuLauncher.Services.Backpack;

public sealed class ArtifactMetaService
{
    private readonly Dictionary<string, (int SetId, string Icon, int MaxRank)> _map;
    private readonly Dictionary<string, Dictionary<string, string>>           _pieces;
    private readonly Dictionary<string, Dictionary<string, string>>           _bonuses;
    private readonly Dictionary<int, Dictionary<string, float[]>>             _mainProps;

    public ArtifactMetaService()
    {
        var dir   = Path.Combine(AppContext.BaseDirectory, "Assets", "Backpack", "Artifact");
        var items = JsonLoader.Load<ArtifactMeta[]>(Path.Combine(dir, "artifacts.json")) ?? [];

        _map = items
            .Where(e => !string.IsNullOrEmpty(e.Name))
            .ToDictionary(
                e => e.Name,
                e => (e.Id, e.Icon, e.LevelList.Length > 0 ? e.LevelList.Max() : 4),
                StringComparer.OrdinalIgnoreCase);
        _pieces = items
            .Where(e => !string.IsNullOrEmpty(e.Name) && e.Pieces is { Count: > 0 })
            .ToDictionary(e => e.Name, e => e.Pieces!, StringComparer.OrdinalIgnoreCase);
        _bonuses = items
            .Where(e => !string.IsNullOrEmpty(e.Name) && e.Bonuses is { Count: > 0 })
            .ToDictionary(e => e.Name, e => e.Bonuses!, StringComparer.OrdinalIgnoreCase);

        var raw = JsonLoader.Load<Dictionary<string, Dictionary<string, float[]>>>(
            Path.Combine(dir, "artifact_main_props.json")) ?? [];
        _mainProps = [];
        foreach (var (key, value) in raw)
            if (int.TryParse(key, out int rank))
                _mainProps[rank] = value;
    }

    public string GetSetBonus(string setName, int pieceCount)
    {
        if (_bonuses.TryGetValue(setName, out var byCount) &&
            byCount.TryGetValue(pieceCount.ToString(), out var desc))
            return desc;
        return string.Empty;
    }

    public IReadOnlyList<(int Count, string Desc)> GetAllSetBonuses(string setName)
    {
        if (!_bonuses.TryGetValue(setName, out var byCount)) return [];
        return [.. byCount
            .Select(kvp => (int.TryParse(kvp.Key, out var n) ? n : 0, kvp.Value))
            .Where(t => t.Item1 > 0 && !string.IsNullOrEmpty(t.Value))
            .OrderBy(t => t.Item1)];
    }

    public string GetPieceName(string setName, string slot)
    {
        if (_pieces.TryGetValue(setName, out var bySlot) && bySlot.TryGetValue(slot, out var name))
            return name;
        return slot;
    }

    public float GetMainPropValue(int rank, int level, string propTypeRaw)
    {
        if (!_mainProps.TryGetValue(rank, out var byProp)) return 0f;
        if (!byProp.TryGetValue(propTypeRaw, out var values)) return 0f;
        var idx = Math.Clamp(level, 0, values.Length - 1);
        return values[idx];
    }

    public Uri? GetIcon(string setName, string slot)
    {
        if (!_map.TryGetValue(setName, out var meta) || string.IsNullOrEmpty(meta.Icon))
            return null;

        var lastUnderscore = meta.Icon.LastIndexOf('_');
        var baseName = lastUnderscore >= 0 ? meta.Icon[..lastUnderscore] : meta.Icon;
        return StaticResources.ArtifactIcon($"{baseName}_{SlotToIndex(slot)}");
    }

    public IReadOnlyList<ArtifactEntry> GetDefaultEntries()
    {
        string[] allSlots =
        [
            BackpackLocalization.Get("SlotFlower"),
            BackpackLocalization.Get("SlotPlume"),
            BackpackLocalization.Get("SlotSands"),
            BackpackLocalization.Get("SlotGoblet"),
            BackpackLocalization.Get("SlotCirclet"),
        ];
        return [.. _map
            .OrderByDescending(kvp => kvp.Value.MaxRank).ThenBy(kvp => kvp.Value.SetId)
            .SelectMany(kvp =>
                GetSlotsForIcon(kvp.Value.Icon, allSlots).Select(slot => new ArtifactEntry(
                    (uint)kvp.Value.SetId,
                    string.Empty,
                    kvp.Key,
                    GetPieceName(kvp.Key, slot),
                    slot,
                    false,
                    0,
                    kvp.Value.MaxRank,
                    new ArtifactMainStat(string.Empty, string.Empty),
                    [])))];
    }

    private static IEnumerable<string> GetSlotsForIcon(string icon, string[] allSlots)
    {
        var lastUnderscore = icon.LastIndexOf('_');
        if (lastUnderscore < 0) return allSlots;
        return icon[(lastUnderscore + 1)..] switch
        {
            "4" => allSlots,
            var s => [IconSuffixToSlot(s)],
        };
    }

    private static string IconSuffixToSlot(string suffix) => suffix switch
    {
        "1" => BackpackLocalization.Get("SlotGoblet"),
        "2" => BackpackLocalization.Get("SlotPlume"),
        "3" => BackpackLocalization.Get("SlotCirclet"),
        "5" => BackpackLocalization.Get("SlotSands"),
        _   => BackpackLocalization.Get("SlotFlower"),
    };

    private static int SlotToIndex(string slot)
    {
        if (slot == BackpackLocalization.Get("SlotFlower"))  return 4;
        if (slot == BackpackLocalization.Get("SlotPlume"))   return 2;
        if (slot == BackpackLocalization.Get("SlotSands"))   return 5;
        if (slot == BackpackLocalization.Get("SlotGoblet"))  return 1;
        if (slot == BackpackLocalization.Get("SlotCirclet")) return 3;
        return 4;
    }

    private sealed record ArtifactMeta(
        [property: JsonPropertyName("id")]        int    Id,
        [property: JsonPropertyName("name")]      string Name,
        [property: JsonPropertyName("levelList")] int[]  LevelList,
        [property: JsonPropertyName("icon")]      string Icon,
        [property: JsonPropertyName("pieces")]    Dictionary<string, string>? Pieces,
        [property: JsonPropertyName("bonuses")]   Dictionary<string, string>? Bonuses
    );
}
