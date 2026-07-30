/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services.Backpack;

public abstract class TabMetaService
{
    private readonly Dictionary<uint, MetaEntry> _map = [];
    private readonly IReadOnlyList<(string Key, string Label, IReadOnlyList<uint> Ids)> _groups;

    protected record MetaEntry(int Id, string Name, string Type, int Rank, string Icon, uint PropId = 0u);

    protected TabMetaService(string subDir, (string File, string Key)[] tabDefs, bool sortByRank = false)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Backpack", subDir);
        var groups = new List<(string Key, string Label, IReadOnlyList<uint> Ids)>(tabDefs.Length);

        foreach (var (file, key) in tabDefs)
        {
            var path = Path.Combine(dir, file);
            var items = JsonLoader.Load<RawEntry[]>(path) ?? [];
            if (items.Length == 0) continue;

            IEnumerable<RawEntry> sequence = items.DistinctBy(x => x.Id);
            if (sortByRank) sequence = sequence.OrderByDescending(x => x.Rank).ThenBy(x => x.Id);

            var ids = new List<uint>();
            foreach (var item in sequence)
            {
                _map[(uint)item.Id] = new MetaEntry(item.Id, item.Name, item.Type, item.Rank, item.Icon, item.PropId);
                ids.Add((uint)item.Id);
            }
            if (ids.Count > 0) groups.Add((key, BackpackLocalization.Get(key), ids));
        }
        _groups = groups;
    }

    public (Uri? IconUri, int Rank) GetMeta(uint id) =>
        _map.TryGetValue(id, out var meta) && !string.IsNullOrEmpty(meta.Icon)
            ? (StaticResources.MaterialIcon(meta.Icon), meta.Rank)
            : (null, 1);

    public string GetName(uint id) => _map.TryGetValue(id, out var entry) ? entry.Name : string.Empty;
    public uint GetPropId(uint id) => _map.TryGetValue(id, out var entry) ? entry.PropId : 0u;
    public IReadOnlyList<(string Key, string Label, IReadOnlyList<uint> Ids)> Groups => _groups;

    private sealed class RawEntry
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("rank")] public int Rank { get; set; }
        [JsonPropertyName("icon")] public string Icon { get; set; } = string.Empty;
        [JsonPropertyName("propId")] public uint PropId { get; set; }
    }
}
