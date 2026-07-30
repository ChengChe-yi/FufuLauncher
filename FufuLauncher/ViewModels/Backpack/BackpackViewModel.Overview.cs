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
        {}
    }

    private void RebuildKpis()
    {
        SafeClear(OverviewKpis);

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

        OverviewKpis.Add(new("\uE7AD", $"{ownedWeapons}", BackpackLocalization.Get("KpiOwnedWeapons"), "accent"));
        OverviewKpis.Add(new("\uECA5", $"{lockedArtifacts}", BackpackLocalization.Get("KpiLockedArtifacts"), "accent"));
        OverviewKpis.Add(new("\uE8B7", $"{cookableCount}", BackpackLocalization.Get("KpiCookableFood"), cookableCount > 0 ? "up" : "muted"));
        OverviewKpis.Add(new("\uE8FD", $"{ownedMaterials}/{totalMaterials}", BackpackLocalization.Get("KpiMaterialTypes"), "accent"));
        OverviewKpis.Add(new("\uE734", $"{fiveStarWeapons}", BackpackLocalization.Get("KpiFiveStarWeapons"), "star5"));
        OverviewKpis.Add(new("\uECA5", $"{totalArtifacts}", BackpackLocalization.Get("KpiTotalArtifacts"), "muted"));
    }

    private void RebuildInsights()
    {
        SafeClear(OverviewInsights);

        var maxRefine = Weapons.Count(w => w.HasInstance && w.Source.Rank == 5 && w.Source.Refine >= 5);
        if (maxRefine > 0)
            OverviewInsights.Add(new("\uE735", BackpackLocalization.Get("InsightMaxRefine.Title"), string.Format(BackpackLocalization.Get("InsightMaxRefine.Body"), maxRefine), "star5"));

        var maxLevelArtifacts = Artifacts.Count(a => a.HasInstance && a.Source.Level == 20 && a.Source.Rank == 5);
        if (maxLevelArtifacts > 0)
            OverviewInsights.Add(new("\uE945", BackpackLocalization.Get("InsightMaxLevel.Title"), string.Format(BackpackLocalization.Get("InsightMaxLevel.Body"), maxLevelArtifacts), "accent"));

        var readyCount = FoodGroups.Sum(g => g.Items.Count(f => f.IsCookable));
        if (readyCount > 0)
            OverviewInsights.Add(new("\uE8B7", BackpackLocalization.Get("InsightIngredientsReady.Title"), string.Format(BackpackLocalization.Get("InsightIngredientsReady.Body"), readyCount), "up"));

        var emptyGroups = MaterialGroups.Where(g => g.Items.All(m => m.CountValue == 0)).ToList();
        if (emptyGroups.Count > 0)
            OverviewInsights.Add(new("\uEA39", BackpackLocalization.Get("InsightEmptyCategories.Title"),
                string.Format(BackpackLocalization.Get("InsightEmptyCategories.Body"), emptyGroups.Count, string.Join(", ", emptyGroups.Take(3).Select(g => g.Header)) + (emptyGroups.Count > 3 ? "..." : string.Empty)),
                "down"));

        var catalogOnlyWeapons = Weapons.Count(w => !w.HasInstance);
        if (catalogOnlyWeapons > 0)
            OverviewInsights.Add(new("\uE7AD", BackpackLocalization.Get("InsightCatalogWeapons.Title"),
                string.Format(BackpackLocalization.Get("InsightCatalogWeapons.Body"), catalogOnlyWeapons), "muted"));

        var lowStock = MaterialGroups.SelectMany(g => g.Items)
            .Count(m => m.CountValue > 0 && m.CountValue < 5);
        if (lowStock > 0)
            OverviewInsights.Add(new("\uE7BA", BackpackLocalization.Get("InsightLowStock.Title"), string.Format(BackpackLocalization.Get("InsightLowStock.Body"), lowStock), "down"));

        if (OverviewInsights.Count == 0)
            OverviewInsights.Add(new("\uE8FB", BackpackLocalization.Get("InsightEmpty.Title"), BackpackLocalization.Get("InsightEmpty.Body"), "muted"));
    }

    private void RebuildCultivation()
    {
        SafeClear(CultivationPlan);
        
        var cultivationGroups = new[] { "MatTabCharAscension", "MatTabWeaponAscension", "MatTabTalent" };
        foreach (var group in MaterialGroups)
        {
            if (!cultivationGroups.Contains(group.Key)) continue;
            var total = group.Items.Count;
            if (total == 0) continue;
            var owned = group.Items.Count(m => m.CountValue > 0);
            var progress = (double)owned / total * 100;
            var color = progress >= 80 ? "up" : progress >= 40 ? "accent" : "down";
            CultivationPlan.Add(new(group.Header, $"{owned}/{total}", progress, color));
        }
        
        var localGroup = MaterialGroups.FirstOrDefault(g => g.Key == "MatTabLocalSpecialty");
        if (localGroup != null)
        {
            var total = localGroup.Items.Count;
            var owned = localGroup.Items.Count(m => m.CountValue > 0);
            if (total > 0)
            {
                var progress = (double)owned / total * 100;
                CultivationPlan.Add(new(localGroup.Header, $"{owned}/{total}", progress,
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
                CultivationPlan.Add(new(BackpackLocalization.Get("PlanHighTierTalent"), $"{owned}/{highTier.Count}", progress,
                    progress >= 50 ? "accent" : "down"));
            }
        }
    }

    private void RebuildCooking()
    {
        SafeClear(CookingItems);

        var allFoods = FoodGroups.SelectMany(g => g.Items).ToList();
        var half = Math.Max(0, BackpackViewModel.PageSize / 2);
        
        var cookable = allFoods.Where(f => f.IsCookable).Take(half).ToList();
        foreach (var food in cookable)
        {
            CookingItems.Add(new(food.Name, BackpackLocalization.Get("CookReady"), food.IngredientsText, true, "up", food.IconUri, food.QualitySource));
        }
        
        var almostCookable = allFoods
            .Where(f => !f.IsCookable && f.Ingredients.Count(i => !i.IsEnough) == 1)
            .Take(half).ToList();
        foreach (var food in almostCookable)
        {
            var missing = food.Ingredients.First(i => !i.IsEnough);
            CookingItems.Add(new(food.Name, BackpackLocalization.Get("CookAlmost"),
                string.Format(BackpackLocalization.Get("CookAlmostBody"), missing.Name, missing.HeldText),
                false, "soon", food.IconUri, food.QualitySource));
        }
        
        if (CookingItems.Count == 0)
        {
            CookingItems.Add(new(BackpackLocalization.Get("CookEmptyName"), BackpackLocalization.Get("CookEmptyStatus"), BackpackLocalization.Get("CookEmptyBody"), false, "muted"));
        }
    }
}
