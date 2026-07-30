/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using FufuLauncher.Helpers;

namespace FufuLauncher.ViewModels;

public sealed partial class BackpackViewModel
{
    public ObservableCollection<BackpackKpiItem> OverviewKpis { get; } = [];
    public ObservableCollection<BackpackInsightItem> OverviewInsights { get; } = [];
    public ObservableCollection<BackpackPlanItem> CultivationPlan { get; } = [];
    public ObservableCollection<BackpackCookingItem> CookingItems { get; } = [];

    public void RebuildOverview()
    {
        InvokeOnUiThread(() =>
        {
            EnsureAllBrowseDataLoaded();
            RebuildKpis();
            RebuildInsights();
            RebuildCultivation();
            RebuildCooking();
        });
    }

    private static void SafeClear<T>(ObservableCollection<T> collection)
    {
        if (collection.Count == 0) return;
        try
        {
            collection.Clear();
        }
        catch (COMException)
        {
            while (collection.Count > 0)
            {
                try { collection.RemoveAt(collection.Count - 1); }
                catch (COMException) { break; }
            }
        }
    }

    private static void SafeReplaceAll<T>(ObservableCollection<T> collection, List<T> items)
    {
        try
        {
            collection.Clear();
        }
        catch (COMException)
        {
            while (collection.Count > 0)
            {
                try { collection.RemoveAt(collection.Count - 1); }
                catch (COMException) { break; }
            }
        }

        foreach (var item in items)
        {
            try { collection.Add(item); }
            catch (COMException) { break; }
        }
    }

    private void RebuildKpis()
    {
        var totalWeapons = Weapons.Count;
        var ownedWeapons = Weapons.Count(w => w.HasInstance);
        var fiveStarWeapons = Weapons.Count(w => w.HasInstance && w.Source.Rank == 5);
        var lockedArtifacts = Artifacts.Count(a => a.Source.Locked);
        var totalArtifacts = Artifacts.Count(a => a.HasInstance);

        var cookableCount = 0;
        var totalFood = 0;
        foreach (var group in FoodGroups)
        {
            totalFood += group.Items.Count;
            cookableCount += group.Items.Count(f => f.IsCookable);
        }

        var ownedMaterials = 0;
        var totalMaterials = 0;
        foreach (var group in MaterialGroups)
        {
            totalMaterials += group.Items.Count;
            ownedMaterials += group.Items.Count(m => m.CountValue > 0);
        }

        var items = new List<BackpackKpiItem>
        {
            new("\uE7AD", $"{ownedWeapons}", BackpackLocalization.Get("KpiOwnedWeapons"), "accent"),
            new("\uECA5", $"{lockedArtifacts}", BackpackLocalization.Get("KpiLockedArtifacts"), "accent"),
            new("\uE8B7", $"{cookableCount}", BackpackLocalization.Get("KpiCookableFood"), cookableCount > 0 ? "up" : "muted"),
            new("\uE8FD", $"{ownedMaterials}/{totalMaterials}", BackpackLocalization.Get("KpiMaterialTypes"), "accent"),
            new("\uE734", $"{fiveStarWeapons}", BackpackLocalization.Get("KpiFiveStarWeapons"), "star5"),
            new("\uECA5", $"{totalArtifacts}", BackpackLocalization.Get("KpiTotalArtifacts"), "muted"),
        };

        SafeReplaceAll(OverviewKpis, items);
    }

    private void RebuildInsights()
    {
        var items = new List<BackpackInsightItem>();

        var maxRefine = Weapons.Count(w => w.HasInstance && w.Source.Rank == 5 && w.Source.Refine >= 5);
        if (maxRefine > 0)
            items.Add(new("\uE735", BackpackLocalization.Get("InsightMaxRefine.Title"), string.Format(BackpackLocalization.Get("InsightMaxRefine.Body"), maxRefine), "star5"));

        var maxLevelArtifacts = Artifacts.Count(a => a.HasInstance && a.Source.Level == 20 && a.Source.Rank == 5);
        if (maxLevelArtifacts > 0)
            items.Add(new("\uE945", BackpackLocalization.Get("InsightMaxLevel.Title"), string.Format(BackpackLocalization.Get("InsightMaxLevel.Body"), maxLevelArtifacts), "accent"));

        var readyCount = FoodGroups.Sum(g => g.Items.Count(f => f.IsCookable));
        if (readyCount > 0)
            items.Add(new("\uE8B7", BackpackLocalization.Get("InsightIngredientsReady.Title"), string.Format(BackpackLocalization.Get("InsightIngredientsReady.Body"), readyCount), "up"));

        var emptyGroups = MaterialGroups.Where(g => g.Items.All(m => m.CountValue == 0)).ToList();
        if (emptyGroups.Count > 0)
            items.Add(new("\uEA39", BackpackLocalization.Get("InsightEmptyCategories.Title"),
                string.Format(BackpackLocalization.Get("InsightEmptyCategories.Body"), emptyGroups.Count, string.Join(", ", emptyGroups.Take(3).Select(g => g.Header)) + (emptyGroups.Count > 3 ? "..." : string.Empty)),
                "down"));

        var catalogOnlyWeapons = Weapons.Count(w => !w.HasInstance);
        if (catalogOnlyWeapons > 0)
            items.Add(new("\uE7AD", BackpackLocalization.Get("InsightCatalogWeapons.Title"),
                string.Format(BackpackLocalization.Get("InsightCatalogWeapons.Body"), catalogOnlyWeapons), "muted"));

        var lowStock = MaterialGroups.SelectMany(g => g.Items)
            .Count(m => m.CountValue > 0 && m.CountValue < 5);
        if (lowStock > 0)
            items.Add(new("\uE7BA", BackpackLocalization.Get("InsightLowStock.Title"), string.Format(BackpackLocalization.Get("InsightLowStock.Body"), lowStock), "down"));

        if (items.Count == 0)
            items.Add(new("\uE8FB", BackpackLocalization.Get("InsightEmpty.Title"), BackpackLocalization.Get("InsightEmpty.Body"), "muted"));

        SafeReplaceAll(OverviewInsights, items);
    }

    private void RebuildCultivation()
    {
        var items = new List<BackpackPlanItem>();

        var cultivationGroups = new[] { "MatTabCharAscension", "MatTabWeaponAscension", "MatTabTalent" };
        foreach (var group in MaterialGroups)
        {
            if (!cultivationGroups.Contains(group.Key)) continue;
            var total = group.Items.Count;
            if (total == 0) continue;
            var owned = group.Items.Count(m => m.CountValue > 0);
            var progress = (double)owned / total * 100;
            var color = progress >= 80 ? "up" : progress >= 40 ? "accent" : "down";
            items.Add(new(group.Header, $"{owned}/{total}", progress, color));
        }
        
        var localGroup = MaterialGroups.FirstOrDefault(g => g.Key == "MatTabLocalSpecialty");
        if (localGroup != null)
        {
            var total = localGroup.Items.Count;
            var owned = localGroup.Items.Count(m => m.CountValue > 0);
            if (total > 0)
            {
                var progress = (double)owned / total * 100;
                items.Add(new(localGroup.Header, $"{owned}/{total}", progress,
                    progress >= 60 ? "up" : "accent"));
            }
        }
        
        var talentGroup = MaterialGroups.FirstOrDefault(g => g.Key == "MatTabTalent");
        if (talentGroup != null)
        {
            var highTier = talentGroup.Items.Where(m => m.Rank >= 4).ToList();
            if (highTier.Count > 0)
            {
                var owned = highTier.Count(m => m.CountValue > 0);
                var progress = (double)owned / highTier.Count * 100;
                items.Add(new(BackpackLocalization.Get("PlanHighTierTalent"), $"{owned}/{highTier.Count}", progress,
                    progress >= 50 ? "accent" : "down"));
            }
        }

        SafeReplaceAll(CultivationPlan, items);
    }

    private void RebuildCooking()
    {
        var items = new List<BackpackCookingItem>();

        var allFoods = FoodGroups.SelectMany(g => g.Items).ToList();
        var half = Math.Max(0, BackpackViewModel.PageSize / 2);
        
        var cookable = allFoods.Where(f => f.IsCookable).Take(half).ToList();
        foreach (var food in cookable)
        {
            items.Add(new(food.Name, BackpackLocalization.Get("CookReady"), food.IngredientsText, true, "up", food.IconUri, food.QualitySource));
        }
        
        var almostCookable = allFoods
            .Where(f => !f.IsCookable && f.Ingredients.Count(i => !i.IsEnough) == 1)
            .Take(half).ToList();
        foreach (var food in almostCookable)
        {
            var missing = food.Ingredients.First(i => !i.IsEnough);
            items.Add(new(food.Name, BackpackLocalization.Get("CookAlmost"),
                string.Format(BackpackLocalization.Get("CookAlmostBody"), missing.Name, missing.HeldText),
                false, "soon", food.IconUri, food.QualitySource));
        }
        
        if (items.Count == 0)
        {
            items.Add(new(BackpackLocalization.Get("CookEmptyName"), BackpackLocalization.Get("CookEmptyStatus"), BackpackLocalization.Get("CookEmptyBody"), false, "muted"));
        }

        SafeReplaceAll(CookingItems, items);
    }
}
