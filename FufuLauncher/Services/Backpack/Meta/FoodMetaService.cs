/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using System.Text.Json.Serialization;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services.Backpack;

public sealed class FoodMetaService
{
    private static readonly (string File, string Key)[] _tabDefs =
    [
        ("foods_recovery.json", "FoodTabRecovery"), ("foods_attack.json", "FoodTabAttack"),
        ("foods_defense.json", "FoodTabDefense"), ("foods_adventure.json", "FoodTabAdventure"),
        ("foods_special_recovery.json", "FoodTabSpecialRecovery"), ("foods_special_attack.json", "FoodTabSpecialAttack"),
        ("foods_special_defense.json", "FoodTabSpecialDefense"), ("foods_special_adventure.json", "FoodTabSpecialAdventure"),
        ("foods_special_arlecchino.json", "FoodTabArlecchino"), ("foods_sweet.json", "FoodTabSweet")
    ];

    private readonly Dictionary<uint, FoodMeta> _map = [];
    private readonly IReadOnlyList<(string Key, string Label, IReadOnlyList<uint> Ids)> _groups;

    public FoodMetaService()
    {
        var foodDir = Path.Combine(AppContext.BaseDirectory, "Assets", "Backpack", "Food");
        var groups = new List<(string Key, string Label, IReadOnlyList<uint> Ids)>(_tabDefs.Length);
        foreach (var (file, key) in _tabDefs)
        {
            try
            {
                var items = JsonLoader.Load<RawEntry[]>(Path.Combine(foodDir, file)) ?? [];
                var ids = new List<uint>(items.Length);
                foreach (var item in items.DistinctBy(x => x.Id))
                {
                    var id = (uint)item.Id;
                    _map[id] = new FoodMeta(item.Name, item.Type, item.Variant, item.Rank, item.Icon,
                        item.Character ?? string.Empty, ParseIngredients(item.IngredientsRaw));
                    ids.Add(id);
                }
                if (ids.Count > 0) groups.Add((key, BackpackLocalization.Get(key), ids));
            }
            catch { }
        }
        _groups = groups;
    }

    private static IReadOnlyList<IngredientMeta> ParseIngredients(JsonElement raw)
    {
        static IngredientMeta ParseOne(JsonElement item) => new((uint)item.GetProperty("id").GetInt32(),
            item.GetProperty("name").GetString() ?? string.Empty, item.GetProperty("amount").GetInt32());
        return raw.ValueKind == JsonValueKind.Array ? [.. raw.EnumerateArray().Select(ParseOne)] : [ParseOne(raw)];
    }

    public FoodMeta? GetMeta(uint id) => _map.GetValueOrDefault(id);
    public IReadOnlyList<(string Key, string Label, IReadOnlyList<uint> Ids)> Groups => _groups;

    public sealed record FoodMeta(string Name, string DishType, string Variant, int Rank, string Icon, string Character,
        IReadOnlyList<IngredientMeta> Ingredients);
    public sealed record IngredientMeta(uint Id, string Name, int Amount);

    private sealed class RawEntry
    {
        [JsonPropertyName("id")] public int Id { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("variant")] public string Variant { get; set; } = string.Empty;
        [JsonPropertyName("rank")] public int Rank { get; set; }
        [JsonPropertyName("icon")] public string Icon { get; set; } = string.Empty;
        [JsonPropertyName("character")] public string? Character { get; set; }
        [JsonPropertyName("ingredients")] public JsonElement IngredientsRaw { get; set; }
    }
}
