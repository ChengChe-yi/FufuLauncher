using FufuLauncher.Models.Backpack;
using FufuLauncher.Services.Backpack;

namespace FufuLauncher.ViewModels;

public sealed partial class BackpackViewModel
{
    private void RebuildMaterialGroups()
    {
        _materialGroupsLoaded = true;
        MaterialGroups.Clear();
        foreach (var (key, label, ids) in _materialMeta.Groups)
        {
            MaterialGroups.Add(new GroupViewModel<MaterialViewModel>(key, label, [.. ids.Select(id =>
            {
                var count = _activeCounts.TryGetValue(id, out var value) ? value : 0UL;
                return new MaterialViewModel(new MaterialEntry(id, _materialMeta.GetName(id), string.Empty, count), _materialMeta);
            })]));
        }
    }

    private void RebuildGadgetGroups()
    {
        _gadgetGroupsLoaded = true;
        GadgetGroups.Clear();
        foreach (var (key, label, ids) in _gadgetMeta.Groups)
        {
            GadgetGroups.Add(new GroupViewModel<GadgetViewModel>(key, label, [.. ids.Select(id =>
            {
                var count = _activeCounts.TryGetValue(id, out var value) ? value : 0UL;
                return new GadgetViewModel(new MaterialEntry(id, _gadgetMeta.GetName(id), string.Empty, count), _gadgetMeta);
            })]));
        }
    }

    private void RebuildAssetGroups()
    {
        _assetGroupsLoaded = true;
        AssetGroups.Clear();
        foreach (var (key, label, ids) in _assetMeta.Groups)
        {
            AssetGroups.Add(new GroupViewModel<AssetViewModel>(key, label, [.. ids.Select(id =>
            {
                var propId = _assetMeta.GetPropId(id);
                var count = propId != 0
                    ? (_activeProps.TryGetValue(propId, out var prop) ? (ulong)Math.Max(0L, prop) : 0UL)
                    : (_activeCounts.TryGetValue(id, out var value) ? value : 0UL);
                return new AssetViewModel(new MaterialEntry(id, _assetMeta.GetName(id), string.Empty, count), _assetMeta);
            })]));
        }
    }

    private void RebuildFoodGroups()
    {
        _foodGroupsLoaded = true;
        FoodGroups.Clear();
        foreach (var (key, label, ids) in _foodMeta.Groups)
        {
            FoodGroups.Add(new GroupViewModel<FoodViewModel>(key, label, [.. ids
                .Select(id => (id, meta: _foodMeta.GetMeta(id)))
                .Where(entry => entry.meta is not null)
                .Select(entry =>
                {
                    var count = _activeCounts.TryGetValue(entry.id, out var value) ? value : 0UL;
                    var ingredients = entry.meta!.Ingredients.Select(ingredient =>
                    {
                        var held = _activeCounts.TryGetValue(ingredient.Id, out var inventory) ? inventory : 0UL;
                        return new IngredientViewModel(ingredient, held, _materialMeta.GetMeta(ingredient.Id).IconUri);
                    }).ToList();
                    return new FoodViewModel(entry.meta!, count, ingredients);
                })]));
        }
    }
}
