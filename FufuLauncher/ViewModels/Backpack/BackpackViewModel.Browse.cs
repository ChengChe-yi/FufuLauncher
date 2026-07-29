using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;

namespace FufuLauncher.ViewModels;

public sealed partial class BackpackViewModel
{
    public ObservableCollection<BackpackBrowseChip> CategoryOptions { get; } =
    [
        new("all", BackpackLocalization.Get("ChipAll"), "\uE71D", true),
        new("weapons", BackpackLocalization.Get("TabWeapon.Header"), "\uE7AD"),
        new("artifacts", BackpackLocalization.Get("TabArtifact.Header"), "\uECA5"),
        new("materials", BackpackLocalization.Get("TabMaterial.Header"), "\uE8FD"),
        new("food", BackpackLocalization.Get("TabFood.Header"), "\uE8B7"),
        new("gadgets", BackpackLocalization.Get("TabGadget.Header"), "\uE950"),
        new("assets", BackpackLocalization.Get("TabAsset.Header"), "\uE734")
    ];

    public ObservableCollection<BackpackBrowseChip> SubcategoryOptions { get; } = [];
    public ObservableCollection<BackpackBrowseChip> FilterOptions { get; } = [];
    public ObservableCollection<BackpackBrowseChip> SortOptions { get; } = [];

    public ObservableCollection<WeaponViewModel> DisplayWeapons { get; } = [];
    public ObservableCollection<ArtifactViewModel> DisplayArtifacts { get; } = [];
    public ObservableCollection<GroupViewModel<MaterialViewModel>> DisplayMaterialGroups { get; } = [];
    public ObservableCollection<GroupViewModel<FoodViewModel>> DisplayFoodGroups { get; } = [];
    public ObservableCollection<GroupViewModel<GadgetViewModel>> DisplayGadgetGroups { get; } = [];
    public ObservableCollection<GroupViewModel<AssetViewModel>> DisplayAssetGroups { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultSummary))]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    [NotifyPropertyChangedFor(nameof(HasNoResultsVisibility))]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubcategoryVisibility))]
    public partial string SelectedCategory { get; set; } = "all";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubcategoryVisibility))]
    public partial string SelectedSubcategory { get; set; } = "all";

    [ObservableProperty]
    public partial string SelectedFilter { get; set; } = "all";

    [ObservableProperty]
    public partial string SelectedSort { get; set; } = "default";

    public int VisibleItemCount => DisplayWeapons.Count + DisplayArtifacts.Count +
        DisplayMaterialGroups.Sum(group => group.Items.Count) + DisplayFoodGroups.Sum(group => group.Items.Count) +
        DisplayGadgetGroups.Sum(group => group.Items.Count) + DisplayAssetGroups.Sum(group => group.Items.Count);
    
    public int TotalVisibleCount { get; private set; }

    public int OwnedWeaponCount => Weapons.Count(item => item.HasInstance);
    public int LockedArtifactCount => Artifacts.Count(item => item.Source.Locked);
    public int CookableFoodCount => FoodGroups.Sum(group => group.Items.Count(item => item.IsCookable));
    public int OwnedMaterialCount => MaterialGroups.Sum(group => group.Items.Count(item => item.CountValue > 0));

    public string ResultSummary
    {
        get
        {
            if (TotalVisibleCount <= PageSize) return string.Format(BackpackLocalization.Get("ResultAll"), TotalVisibleCount.ToString("N0"));
            var first = (CurrentPage - 1) * PageSize + 1;
            var last = Math.Min(CurrentPage * PageSize, TotalVisibleCount);
            return string.Format(BackpackLocalization.Get("PagerRange"), first, last, TotalVisibleCount.ToString("N0"));
        }
    }
    public string OwnedWeaponSummary => $"{OwnedWeaponCount:N0} 把";
    public string LockedArtifactSummary => $"{LockedArtifactCount:N0} 件";
    public string CookableFoodSummary => $"{CookableFoodCount:N0} 道";
    public string OwnedMaterialSummary => $"{OwnedMaterialCount:N0} 项";
    public bool HasNoResults => !IsInitializing && HasSelectedPath && TotalVisibleCount == 0;
    public Visibility HasNoResultsVisibility => HasNoResults.ToVisibility();
    public Visibility SubcategoryVisibility => (SelectedCategory != "all" && SubcategoryOptions.Count > 1).ToVisibility();
    public Visibility WeaponsVisibility => (TotalWeaponCount > 0).ToVisibility();
    public Visibility ArtifactsVisibility => (TotalArtifactCount > 0).ToVisibility();
    public Visibility MaterialsVisibility => (TotalMaterialCount > 0).ToVisibility();
    public Visibility FoodVisibility => (TotalFoodCount > 0).ToVisibility();
    public Visibility GadgetsVisibility => (TotalGadgetCount > 0).ToVisibility();
    public Visibility AssetsVisibility => (TotalAssetCount > 0).ToVisibility();

    partial void OnSearchTextChanged(string value) { CurrentPage = 1; InvokeOnUiThread(ApplyBrowse); }
    partial void OnSelectedCategoryChanged(string value) { CurrentPage = 1; InvokeOnUiThread(ApplyBrowse); }
    partial void OnSelectedSubcategoryChanged(string value) { CurrentPage = 1; InvokeOnUiThread(ApplyBrowse); }
    partial void OnSelectedFilterChanged(string value) { CurrentPage = 1; InvokeOnUiThread(ApplyBrowse); }
    partial void OnSelectedSortChanged(string value) { CurrentPage = 1; InvokeOnUiThread(ApplyBrowse); }

    public void SetSearch(string? search) => SearchText = search?.Trim() ?? string.Empty;

    public void SetCategory(BackpackBrowseChip? chip)
    {
        SelectedCategory = chip?.Key ?? "all";
        SyncSelection(CategoryOptions, SelectedCategory);
    }

    public void SetSubcategory(BackpackBrowseChip? chip)
    {
        SelectedSubcategory = chip?.Key ?? "all";
        SyncSelection(SubcategoryOptions, SelectedSubcategory);
    }

    public void SetFilter(BackpackBrowseChip? chip)
    {
        SelectedFilter = chip?.Key ?? "all";
        SyncSelection(FilterOptions, SelectedFilter);
    }

    public void SetSort(BackpackBrowseChip? chip)
    {
        SelectedSort = chip?.Key ?? "default";
        SyncSelection(SortOptions, SelectedSort);
    }

    public void ResetBrowse()
    {
        SearchText = string.Empty;
        SelectedCategory = "all";
        SelectedSubcategory = "all";
        SelectedFilter = "all";
        SelectedSort = "default";
        SyncSelection(CategoryOptions, SelectedCategory);
    }

    public void RefreshBrowse()
    {
        CurrentPage = 1;
        InvokeOnUiThread(ApplyBrowse);
    }

    // Re-entrancy guard for ApplyBrowse. Setting SelectedCategory below fires
    // OnSelectedCategoryChanged → ApplyBrowse recursively; without this guard the
    // nested call mutates the same ObservableCollections while the outer call's
    // CollectionChanged notifications are still propagating to the native binding
    // layer, producing E_FAIL from NotifyCollectionChangedEventHandler. The outer
    // call already applies the latest SelectedCategory and chip state, so dropping
    // the nested call is safe.
    private bool _isApplyingBrowse;

    private void ApplyBrowse()
    {
        if (_isApplyingBrowse) return;
        _isApplyingBrowse = true;
        try
        {
            EnsureAllBrowseDataLoaded();

            SelectedCategory = CurrentTab switch
            {
                BackpackTab.Weapons => "weapons",
                BackpackTab.Artifacts => "artifacts",
                BackpackTab.Materials => "materials",
                BackpackTab.Food => "food",
                BackpackTab.Gadgets => "gadgets",
                BackpackTab.Assets => "assets",
                _ => "all"
            };

            RefreshContextChips();
            var search = SearchText.Trim();

            var filteredWeapons = new List<WeaponViewModel>();
            var filteredArtifacts = new List<ArtifactViewModel>();
            var filteredMaterialGroups = new List<GroupViewModel<MaterialViewModel>>();
            var filteredFoodGroups = new List<GroupViewModel<FoodViewModel>>();
            var filteredGadgetGroups = new List<GroupViewModel<GadgetViewModel>>();
            var filteredAssetGroups = new List<GroupViewModel<AssetViewModel>>();

            switch (CurrentTab)
            {
                case BackpackTab.Weapons:
                    filteredWeapons.AddRange(SortWeapons(Weapons.Where(item => MatchesWeapon(item, search))));
                    break;
                case BackpackTab.Artifacts:
                    filteredArtifacts.AddRange(SortArtifacts(Artifacts.Where(item => MatchesArtifact(item, search))));
                    break;
                case BackpackTab.Materials:
                    filteredMaterialGroups.AddRange(FilterSimpleGroups(MaterialGroups, search));
                    break;
                case BackpackTab.Food:
                    filteredFoodGroups.AddRange(FilterFoodGroups(FoodGroups, search));
                    break;
                case BackpackTab.Gadgets:
                    filteredGadgetGroups.AddRange(FilterSimpleGroups(GadgetGroups, search));
                    break;
                case BackpackTab.Assets:
                    filteredAssetGroups.AddRange(FilterSimpleGroups(AssetGroups, search));
                    break;
            }

            TotalWeaponCount = filteredWeapons.Count;
            TotalArtifactCount = filteredArtifacts.Count;
            TotalMaterialCount = filteredMaterialGroups.Count;
            TotalFoodCount = filteredFoodGroups.Count;
            TotalGadgetCount = filteredGadgetGroups.Count;
            TotalAssetCount = filteredAssetGroups.Count;

            Replace(DisplayWeapons, Paginate(filteredWeapons, CurrentPage));
            Replace(DisplayArtifacts, Paginate(filteredArtifacts, CurrentPage));
            ReplaceGroupedItems(DisplayMaterialGroups, filteredMaterialGroups, CurrentPage);
            ReplaceGroupedItems(DisplayFoodGroups, filteredFoodGroups, CurrentPage);
            ReplaceGroupedItems(DisplayGadgetGroups, filteredGadgetGroups, CurrentPage);
            ReplaceGroupedItems(DisplayAssetGroups, filteredAssetGroups, CurrentPage);

            TotalVisibleCount = CurrentTab switch
            {
                BackpackTab.Weapons => TotalWeaponCount,
                BackpackTab.Artifacts => TotalArtifactCount,
                BackpackTab.Materials => filteredMaterialGroups.Sum(g => g.Items.Count),
                BackpackTab.Food => filteredFoodGroups.Sum(g => g.Items.Count),
                BackpackTab.Gadgets => filteredGadgetGroups.Sum(g => g.Items.Count),
                BackpackTab.Assets => filteredAssetGroups.Sum(g => g.Items.Count),
                _ => 0
            };

            var totalPagesForCategory = Math.Max(1, (int)Math.Ceiling(TotalVisibleCount / (double)PageSize));
            if (CurrentPage > totalPagesForCategory) CurrentPage = totalPagesForCategory;
            if (CurrentPage < 1) CurrentPage = 1;

            NotifyBrowseChanged();
        }
        finally
        {
            _isApplyingBrowse = false;
        }
    }

    private void EnsureAllBrowseDataLoaded()
    {
        if (!_artifactsLoaded)
        {
            _artifactsLoaded = true;
            foreach (var entry in _initialArtifacts) Artifacts.Add(new ArtifactViewModel(entry, _artifactMeta));
        }
        if (!_materialGroupsLoaded) RebuildMaterialGroups();
        if (!_foodGroupsLoaded) RebuildFoodGroups();
        if (!_gadgetGroupsLoaded) RebuildGadgetGroups();
        if (!_assetGroupsLoaded) RebuildAssetGroups();
    }

    private void RefreshContextChips()
    {
        var subcategories = BuildSubcategories();
        ReplaceChips(SubcategoryOptions, subcategories);
        if (!SubcategoryOptions.Any(chip => chip.Key == SelectedSubcategory))
            SelectedSubcategory = "all";
        SyncSelection(SubcategoryOptions, SelectedSubcategory);

        var filters = BuildFilters();
        ReplaceChips(FilterOptions, filters);
        if (!FilterOptions.Any(chip => chip.Key == SelectedFilter))
            SelectedFilter = "all";
        SyncSelection(FilterOptions, SelectedFilter);

        var sorts = BuildSorts();
        ReplaceChips(SortOptions, sorts);
        if (!SortOptions.Any(chip => chip.Key == SelectedSort))
            SelectedSort = "default";
        SyncSelection(SortOptions, SelectedSort);
        OnPropertyChanged(nameof(SubcategoryVisibility));
    }

    private IEnumerable<BackpackBrowseChip> BuildSubcategories()
    {
        if (SelectedCategory == "all") return [];
        var chips = new List<BackpackBrowseChip> { new("all", string.Format(BackpackLocalization.Get("ChipSubcategoryTemplate"), CategoryLabel(SelectedCategory))) };
        switch (SelectedCategory)
        {
            case "weapons":
                chips.AddRange(Weapons.Select(item => item.Source.Type).Where(type => !string.IsNullOrWhiteSpace(type)).Distinct()
                    .OrderBy(type => type).Select(type => new BackpackBrowseChip(type, type)));
                break;
            case "artifacts":
                chips.AddRange(Artifacts.Select(item => item.Source.Slot).Where(slot => !string.IsNullOrWhiteSpace(slot)).Distinct()
                    .Select(slot => new BackpackBrowseChip(slot, slot)));
                break;
            case "materials": chips.AddRange(MaterialGroups.Select(group => new BackpackBrowseChip(group.Key, group.Header))); break;
            case "food": chips.AddRange(FoodGroups.Select(group => new BackpackBrowseChip(group.Key, group.Header))); break;
            case "gadgets": chips.AddRange(GadgetGroups.Select(group => new BackpackBrowseChip(group.Key, group.Header))); break;
            case "assets": chips.AddRange(AssetGroups.Select(group => new BackpackBrowseChip(group.Key, group.Header))); break;
        }
        return chips;
    }

    private IEnumerable<BackpackBrowseChip> BuildFilters() => SelectedCategory switch
    {
        "weapons" => [new("all", BackpackLocalization.Get("ChipAll")), new("owned", BackpackLocalization.Get("ChipFilterOwned")), new("catalog", BackpackLocalization.Get("ChipFilterCatalog")), new("five", BackpackLocalization.Get("ChipFilterFive"))],
        "artifacts" => [new("all", BackpackLocalization.Get("ChipAll")), new("owned", BackpackLocalization.Get("ChipFilterOwned")), new("catalog", BackpackLocalization.Get("ChipFilterCatalog")), new("five", BackpackLocalization.Get("ChipFilterFive")), new("locked", BackpackLocalization.Get("ChipFilterLocked"))],
        "food" => [new("all", BackpackLocalization.Get("ChipAll")), new("owned", BackpackLocalization.Get("ChipFilterOwned")), new("cookable", BackpackLocalization.Get("ChipFilterCookable"))],
        _ => [new("all", BackpackLocalization.Get("ChipAll")), new("owned", BackpackLocalization.Get("ChipFilterOwned"))]
    };

    private IEnumerable<BackpackBrowseChip> BuildSorts() => SelectedCategory switch
    {
        "weapons" or "artifacts" => [new("default", BackpackLocalization.Get("ChipSortDefault")), new("name", BackpackLocalization.Get("ChipSortName")), new("rank", BackpackLocalization.Get("ChipSortRank")), new("level", BackpackLocalization.Get("ChipSortLevel"))],
        _ => [new("default", BackpackLocalization.Get("ChipSortDefault")), new("name", BackpackLocalization.Get("ChipSortName")), new("rank", BackpackLocalization.Get("ChipSortRank")), new("count", BackpackLocalization.Get("ChipSortCount"))]
    };

    private static string CategoryLabel(string key) => key switch
    {
        "weapons" => BackpackLocalization.Get("TabWeapon.Header"),
        "artifacts" => BackpackLocalization.Get("TabArtifact.Header"),
        "materials" => BackpackLocalization.Get("TabMaterial.Header"),
        "food" => BackpackLocalization.Get("TabFood.Header"),
        "gadgets" => BackpackLocalization.Get("TabGadget.Header"),
        "assets" => BackpackLocalization.Get("TabAsset.Header"),
        _ => BackpackLocalization.Get("ChipCategoryGeneric")
    };

    private bool IsCategoryVisible(string category) => CurrentTab switch
    {
        BackpackTab.Overview => false,
        BackpackTab.Weapons => category == "weapons",
        BackpackTab.Artifacts => category == "artifacts",
        BackpackTab.Materials => category == "materials",
        BackpackTab.Food => category == "food",
        BackpackTab.Gadgets => category == "gadgets",
        BackpackTab.Assets => category == "assets",
        _ => SelectedCategory == "all" || SelectedCategory == category
    };
    private bool MatchesSubcategory(string key) => SelectedSubcategory == "all" || SelectedSubcategory == key;

    private bool MatchesWeapon(WeaponViewModel item, string search)
    {
        if (!MatchesSubcategory(item.Source.Type) ||
            SelectedFilter == "owned" && !item.HasInstance || SelectedFilter == "catalog" && item.HasInstance ||
            SelectedFilter == "five" && item.Source.Rank != 5) return false;
        return string.IsNullOrEmpty(search) || Contains(item.Source.Name, search) || Contains(item.Source.Type, search) ||
               Contains(item.Source.SpecialProp, search) || Contains(item.PassiveName, search);
    }

    private bool MatchesArtifact(ArtifactViewModel item, string search)
    {
        if (!MatchesSubcategory(item.Source.Slot) ||
            SelectedFilter == "owned" && !item.HasInstance || SelectedFilter == "catalog" && item.HasInstance ||
            SelectedFilter == "locked" && !item.Source.Locked || SelectedFilter == "five" && item.Source.Rank != 5) return false;
        return string.IsNullOrEmpty(search) || Contains(item.Source.SetName, search) || Contains(item.Source.Name, search) ||
               Contains(item.Source.Slot, search) || Contains(item.Source.MainStat.Type, search) ||
               item.Source.SubStats.Any(stat => Contains(stat.Type, search));
    }

    private IEnumerable<WeaponViewModel> SortWeapons(IEnumerable<WeaponViewModel> items) => SelectedSort switch
    {
        "name" => items.OrderBy(item => item.Source.Name),
        "rank" => items.OrderByDescending(item => item.Source.Rank).ThenBy(item => item.Source.Name),
        "level" => items.OrderByDescending(item => item.Source.Level).ThenByDescending(item => item.Source.Refine),
        _ => items
    };

    private IEnumerable<ArtifactViewModel> SortArtifacts(IEnumerable<ArtifactViewModel> items) => SelectedSort switch
    {
        "name" => items.OrderBy(item => item.Source.SetName).ThenBy(item => item.Source.Name),
        "rank" => items.OrderByDescending(item => item.Source.Rank).ThenByDescending(item => item.Source.Level),
        "level" => items.OrderByDescending(item => item.Source.Level).ThenByDescending(item => item.Source.Rank),
        _ => items
    };

    private IEnumerable<GroupViewModel<TItem>> FilterSimpleGroups<TItem>(IEnumerable<GroupViewModel<TItem>> groups, string search)
        where TItem : SimpleItemViewModel
    {
        return groups.Where(group => MatchesSubcategory(group.Key)).Select(group =>
        {
            var items = group.Items.Where(item => (SelectedFilter != "owned" || item.CountValue > 0) &&
                (string.IsNullOrEmpty(search) || Contains(item.Name, search)));
            return new GroupViewModel<TItem>(group.Key, group.Header, SortSimple(items).ToList());
        }).Where(group => group.Items.Count > 0);
    }

    private IEnumerable<GroupViewModel<FoodViewModel>> FilterFoodGroups(IEnumerable<GroupViewModel<FoodViewModel>> groups, string search)
    {
        return groups.Where(group => MatchesSubcategory(group.Key)).Select(group =>
        {
            var items = group.Items.Where(item => (SelectedFilter != "owned" || item.CountValue > 0) &&
                (SelectedFilter != "cookable" || item.IsCookable) &&
                (string.IsNullOrEmpty(search) || Contains(item.Name, search) || Contains(item.Character, search) ||
                 item.Ingredients.Any(ingredient => Contains(ingredient.Name, search))));
            return new GroupViewModel<FoodViewModel>(group.Key, group.Header, SortFood(items).ToList());
        }).Where(group => group.Items.Count > 0);
    }

    private IEnumerable<TItem> SortSimple<TItem>(IEnumerable<TItem> items) where TItem : SimpleItemViewModel => SelectedSort switch
    {
        "name" => items.OrderBy(item => item.Name),
        "rank" => items.OrderByDescending(item => item.Rank).ThenBy(item => item.Name),
        "count" => items.OrderByDescending(item => item.CountValue).ThenBy(item => item.Name),
        _ => items
    };

    private IEnumerable<FoodViewModel> SortFood(IEnumerable<FoodViewModel> items) => SelectedSort switch
    {
        "name" => items.OrderBy(item => item.Name),
        "rank" => items.OrderByDescending(item => item.Rank).ThenBy(item => item.Name),
        "count" => items.OrderByDescending(item => item.CountValue).ThenBy(item => item.Name),
        _ => items
    };

    private static void ReplaceChips(ObservableCollection<BackpackBrowseChip> target, IEnumerable<BackpackBrowseChip> chips)
    {
        SafeClear(target);
        foreach (var chip in chips) target.Add(chip);
    }

    private static void SyncSelection(IEnumerable<BackpackBrowseChip> chips, string selectedKey)
    {
        foreach (var chip in chips) chip.IsSelected = chip.Key == selectedKey;
    }

    private void NotifyBrowseChanged()
    {
        foreach (var property in new[]
                 {
                     nameof(VisibleItemCount), nameof(TotalVisibleCount), nameof(TotalPages), nameof(PagerVisibility),
                     nameof(CanPreviousPage), nameof(CanNextPage), nameof(PageCounterText), nameof(ResultSummary),
                     nameof(HasNoResults), nameof(HasNoResultsVisibility),
                     nameof(TotalWeaponCount), nameof(TotalArtifactCount), nameof(TotalMaterialCount),
                     nameof(TotalFoodCount), nameof(TotalGadgetCount), nameof(TotalAssetCount),
                     nameof(WeaponsVisibility), nameof(ArtifactsVisibility), nameof(MaterialsVisibility), nameof(FoodVisibility),
                     nameof(GadgetsVisibility), nameof(AssetsVisibility), nameof(OwnedWeaponCount), nameof(LockedArtifactCount),
                     nameof(CookableFoodCount), nameof(OwnedMaterialCount), nameof(OwnedWeaponSummary), nameof(LockedArtifactSummary),
                     nameof(CookableFoodSummary), nameof(OwnedMaterialSummary)
                 }) OnPropertyChanged(property);
    }

    private static bool Contains(string? source, string value) => source?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        SafeClear(target);
        foreach (var item in items) target.Add(item);
    }
    
    public static IEnumerable<T> Paginate<T>(IList<T> source, int page)
    {
        if (source.Count == 0) yield break;
        var start = (page - 1) * PageSize;
        if (start >= source.Count) yield break;
        var end = Math.Min(start + PageSize, source.Count);
        for (var i = start; i < end; i++) yield return source[i];
    }
    
    private static void ReplaceGroupedItems<TItem>(
        ObservableCollection<GroupViewModel<TItem>> target,
        IList<GroupViewModel<TItem>> allGroups,
        int page)
    {
        SafeClear(target);
        if (allGroups.Count == 0) return;

        var skip = Math.Max(0, (page - 1) * PageSize);
        var remaining = PageSize;
        foreach (var group in allGroups)
        {
            if (remaining <= 0) break;
            var bucket = new List<TItem>();
            foreach (var item in group.Items)
            {
                if (skip > 0) { skip--; continue; }
                if (remaining <= 0) break;
                bucket.Add(item);
                remaining--;
            }
            if (bucket.Count > 0)
                target.Add(new GroupViewModel<TItem>(group.Key, group.Header, bucket));
        }
    }
}
