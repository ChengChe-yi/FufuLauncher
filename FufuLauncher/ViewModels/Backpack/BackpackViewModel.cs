/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using FufuLauncher.Helpers;
using FufuLauncher.Models.Backpack;
using FufuLauncher.Services.Backpack;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FufuLauncher.ViewModels;

public enum BackpackTab { Overview, Weapons, Artifacts, Materials, Food, Gadgets, Assets }

public sealed partial class BackpackViewModel : ObservableObject
{
    public const int PageSize = 12;
    public const int GroupsPerPage = 3;
    private readonly DispatcherQueue      _dispatcher;

    public DispatcherQueue Dispatcher => _dispatcher;
    private readonly MaterialMetaService  _materialMeta;
    private readonly FoodMetaService      _foodMeta;
    private readonly WeaponMetaService    _weaponMeta;
    private readonly ArtifactMetaService  _artifactMeta;
    private readonly GadgetMetaService    _gadgetMeta;
    private readonly AssetMetaService     _assetMeta;
    private readonly BackpackDbService    _db;
    private readonly IReadOnlyList<ArtifactEntry> _initialArtifacts;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverview))]
    [NotifyPropertyChangedFor(nameof(IsWeapons))]
    [NotifyPropertyChangedFor(nameof(IsArtifacts))]
    [NotifyPropertyChangedFor(nameof(IsMaterials))]
    [NotifyPropertyChangedFor(nameof(IsFood))]
    [NotifyPropertyChangedFor(nameof(IsGadgets))]
    [NotifyPropertyChangedFor(nameof(IsAssets))]
    [NotifyPropertyChangedFor(nameof(BrowseVisibility))]
    [NotifyPropertyChangedFor(nameof(OverviewVisibility))]
    public partial BackpackTab CurrentTab { get; set; } = BackpackTab.Overview;

    public bool IsOverview  => CurrentTab == BackpackTab.Overview;
    public bool IsWeapons   => CurrentTab == BackpackTab.Weapons;
    public bool IsArtifacts => CurrentTab == BackpackTab.Artifacts;
    public bool IsMaterials => CurrentTab == BackpackTab.Materials;
    public bool IsFood      => CurrentTab == BackpackTab.Food;
    public bool IsGadgets   => CurrentTab == BackpackTab.Gadgets;
    public bool IsAssets    => CurrentTab == BackpackTab.Assets;

    public Visibility OverviewVisibility => IsOverview.ToVisibility();
    public Visibility BrowseVisibility   => (!IsOverview).ToVisibility();

    public void SetTab(BackpackTab tab)
    {
        if (CurrentTab == tab) return;
        CurrentTab = tab;
        CurrentPage = 1;
        if (tab == BackpackTab.Overview)
            RebuildOverview();
        else
            InvokeOnUiThread(ApplyBrowse);
    }
    
    public string PageTitle => BackpackLocalization.Get("NavTitle");
    public string GamePathDisplay => string.IsNullOrWhiteSpace(GameInstallationPath)
        ? BackpackLocalization.Get("GamePathFallback")
        : GameInstallationPath;
    public string WeaponTabLabel => "Backpack_TabWeapon.Header".GetLocalized();
    public string ArtifactTabLabel => "Backpack_TabArtifact.Header".GetLocalized();
    public string MaterialTabLabel => "Backpack_TabMaterial.Header".GetLocalized();
    public string FoodTabLabel => "Backpack_TabFood.Header".GetLocalized();
    public string GadgetTabLabel => "Backpack_TabGadget.Header".GetLocalized();
    public string AssetTabLabel => "Backpack_TabAsset.Header".GetLocalized();
    public string SelectPathLabel => "Backpack_BtnSelectPath.Label".GetLocalized();
    public string KillGameLabel => "Backpack_BtnKillGame.Label".GetLocalized();
    public string SyncBagLabel => "Backpack_BtnSyncBag.Label".GetLocalized();
    public string SelectedPathDisplay => GamePathDisplay;

    public ObservableCollection<WeaponViewModel>        Weapons        { get; } = [];
    public ObservableCollection<ArtifactViewModel>      Artifacts      { get; } = [];
    public ObservableCollection<GroupViewModel<MaterialViewModel>>  MaterialGroups { get; } = [];
    public ObservableCollection<GroupViewModel<FoodViewModel>>      FoodGroups     { get; } = [];
    public ObservableCollection<GroupViewModel<GadgetViewModel>>    GadgetGroups   { get; } = [];
    public ObservableCollection<GroupViewModel<AssetViewModel>>     AssetGroups    { get; } = [];

    private readonly Dictionary<uint, ulong> _activeCounts = [];
    private readonly Dictionary<uint, long>  _activeProps  = [];
    private bool _artifactsLoaded;
    private bool _materialGroupsLoaded;
    private bool _foodGroupsLoaded;
    private bool _gadgetGroupsLoaded;
    private bool _assetGroupsLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DataVisibility))]
    [NotifyPropertyChangedFor(nameof(SetupVisibility))]
    [NotifyPropertyChangedFor(nameof(ProgressRingVisibility))]
    [NotifyPropertyChangedFor(nameof(LaunchButtonVisibility))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(SelectedPathDisplay))]
    [NotifyPropertyChangedFor(nameof(InitializationVisibility))]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    [NotifyPropertyChangedFor(nameof(HasNoResultsVisibility))]
    [NotifyPropertyChangedFor(nameof(GlobalPathRequiredVisibility))]
    public partial bool HasSelectedPath { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GamePathDisplay))]
    public partial string GameInstallationPath { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProgressRingVisibility))]
    [NotifyPropertyChangedFor(nameof(SyncIconVisibility))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    public partial bool IsLaunching { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(InitializationVisibility))]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(HasNoResults))]
    [NotifyPropertyChangedFor(nameof(HasNoResultsVisibility))]
    public partial bool IsInitializing { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GameStatusLabel))]
    public partial bool IsGameRunning { get; set; } = false;

    [ObservableProperty]
    public partial string StatusText { get; set; } = BackpackLocalization.Get("StatusWaiting");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SetupErrorVisibility))]
    [NotifyPropertyChangedFor(nameof(HasSetupError))]
    public partial string SetupError { get; set; } = string.Empty;

    public Visibility DataVisibility         => Visibility.Visible;
    public Visibility SetupVisibility        => Visibility.Collapsed;
    public Visibility GlobalPathRequiredVisibility => HasSelectedPath.ToCollapsed();
    public Visibility ProgressRingVisibility => (HasSelectedPath && IsLaunching).ToVisibility();
    public Visibility SyncIconVisibility     => IsLaunching.ToCollapsed();
    public Visibility InitializationVisibility => (HasSelectedPath && IsInitializing).ToVisibility();
    public Visibility PathListVisibility     => Visibility.Collapsed;
    public Visibility SetupErrorVisibility   => HasSetupError.ToVisibility();
    public Visibility LaunchButtonVisibility => HasSelectedPath.ToVisibility();
    public bool       HasSetupError           => !string.IsNullOrEmpty(SetupError);
    public bool       CanLaunch              => HasSelectedPath && !IsLaunching && !IsInitializing;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ResultSummary))]
    [NotifyPropertyChangedFor(nameof(PagerVisibility))]
    [NotifyPropertyChangedFor(nameof(CanPreviousPage))]
    [NotifyPropertyChangedFor(nameof(CanNextPage))]
    [NotifyPropertyChangedFor(nameof(PageCounterText))]
    [NotifyPropertyChangedFor(nameof(TotalPages))]
    public partial int CurrentPage { get; set; } = 1;

    partial void OnCurrentPageChanged(int value)
    {
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PagerVisibility));
        OnPropertyChanged(nameof(CanPreviousPage));
        OnPropertyChanged(nameof(CanNextPage));
        OnPropertyChanged(nameof(PageCounterText));
        OnPropertyChanged(nameof(ResultSummary));
    }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalVisibleCount / (double)PageSize));
    public bool CanPreviousPage => CurrentPage > 1;
    public bool CanNextPage => CurrentPage < TotalPages;
    public Visibility PagerVisibility => (TotalPages > 1).ToVisibility();
    public string PageCounterText => string.Format(BackpackLocalization.Get("PagerFormat"), CurrentPage, TotalPages);
    
    [ObservableProperty] public partial int TotalWeaponCount { get; set; }
    [ObservableProperty] public partial int TotalArtifactCount { get; set; }
    [ObservableProperty] public partial int TotalMaterialCount { get; set; }
    [ObservableProperty] public partial int TotalFoodCount { get; set; }
    [ObservableProperty] public partial int TotalGadgetCount { get; set; }
    [ObservableProperty] public partial int TotalAssetCount { get; set; }

    public void GoToPage(int page)
    {
        if (page < 1) page = 1;
        if (page > TotalPages) page = TotalPages;
        if (page == CurrentPage) return;
        CurrentPage = page;
        InvokeOnUiThread(ApplyBrowse);
    }

    public void NextPage() => GoToPage(CurrentPage + 1);
    public void PreviousPage() => GoToPage(CurrentPage - 1);

    public BackpackViewModel(DispatcherQueue dispatcher,
        MaterialMetaService materialMeta, FoodMetaService foodMeta, WeaponMetaService weaponMeta,
        ArtifactMetaService artifactMeta, GadgetMetaService gadgetMeta, AssetMetaService assetMeta, BackpackDbService db)
    {
        _dispatcher   = dispatcher;
        _materialMeta = materialMeta;
        _foodMeta     = foodMeta;
        _weaponMeta   = weaponMeta;
        _artifactMeta = artifactMeta;
        _gadgetMeta   = gadgetMeta;
        _assetMeta    = assetMeta;
        _db           = db;

        var dbWeapons = db.LoadWeapons();
        if (dbWeapons.Count > 0)
            foreach (var e in dbWeapons) Weapons.Add(new WeaponViewModel(e, _weaponMeta));
        else
            LoadDefaultWeapons();

        var dbArtifacts = db.LoadArtifacts();
        _initialArtifacts = dbArtifacts.Count > 0
            ? dbArtifacts
            : _artifactMeta.GetDefaultEntries().ToList();

        _activeCounts = db.LoadMaterialCounts();
        _activeProps  = db.LoadProps();
        RefreshBrowse();
        InitializeWindowCollections();
    }

    public void UpdateGameInstallation(string? directory, bool isAvailable)
    {
        GameInstallationPath = directory?.Trim() ?? string.Empty;
        HasSelectedPath = isAvailable;
    }

    private void LoadDefaultWeapons()
    {
        foreach (var e in _weaponMeta.GetDefaultEntries())
            Weapons.Add(new WeaponViewModel(e, _weaponMeta));
    }
    
    private void InvokeOnUiThread(Action action)
    {
        if (_dispatcher is null) { action(); return; }
        if (_dispatcher.HasThreadAccess) { action(); return; }
        _dispatcher.TryEnqueue(DispatcherQueuePriority.Normal, () => action());
    }
}
