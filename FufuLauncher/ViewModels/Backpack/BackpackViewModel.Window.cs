/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;

namespace FufuLauncher.ViewModels;

public sealed partial class BackpackViewModel
{
    [ObservableProperty]
    public partial BackpackTab SelectedTab { get; set; } = BackpackTab.Overview;

    partial void OnSelectedTabChanged(BackpackTab value)
    {
        if (CurrentTab == value) return;
        SetTab(value);
        SelectedItem = null;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailVisibility))]
    [NotifyPropertyChangedFor(nameof(WeaponDetailVisibility))]
    [NotifyPropertyChangedFor(nameof(ArtifactDetailVisibility))]
    [NotifyPropertyChangedFor(nameof(FoodDetailVisibility))]
    [NotifyPropertyChangedFor(nameof(SimpleDetailVisibility))]
    [NotifyPropertyChangedFor(nameof(DetailTitle))]
    [NotifyPropertyChangedFor(nameof(DetailSubtitle))]
    [NotifyPropertyChangedFor(nameof(DetailGlyph))]
    public partial object? SelectedItem { get; set; }

    [ObservableProperty]
    public partial bool HideEmpty { get; set; }

    partial void OnHideEmptyChanged(bool value)
    {
        CurrentPage = 1;
        InvokeOnUiThread(ApplyBrowse);
    }

    public ObservableCollection<BackpackSidebarEntry> SidebarTabs { get; } = new();

    public ObservableCollection<BackpackSummaryStat> SummaryStats { get; } = new();

    public bool DetailPaneVisible => SelectedItem is not null;
    public Visibility DetailVisibility => DetailPaneVisible.ToVisibility();
    public Visibility MainContentVisibility => DetailPaneVisible.ToCollapsed();

    public Visibility WeaponDetailVisibility  => SelectedItem is WeaponViewModel ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ArtifactDetailVisibility=> SelectedItem is ArtifactViewModel ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FoodDetailVisibility    => SelectedItem is FoodViewModel ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SimpleDetailVisibility  => SelectedItem is SimpleItemViewModel ? Visibility.Visible : Visibility.Collapsed;

    public string DetailTitle
    {
        get
        {
            return SelectedItem switch
            {
                WeaponViewModel w => w.Source.Name,
                ArtifactViewModel a => a.Source.SetName,
                FoodViewModel f => f.Name,
                SimpleItemViewModel s => s.Name,
                _ => "BackpackWindow_DetailTitle".GetLocalized()
            };
        }
    }

    public string DetailSubtitle
    {
        get
        {
            return SelectedItem switch
            {
                WeaponViewModel w => w.TypeRankDisplay,
                ArtifactViewModel a => a.Source.Slot,
                FoodViewModel f => f.Character,
                SimpleItemViewModel => "BackpackWindow_DetailSubtitleSimple".GetLocalized(),
                _ => string.Empty
            };
        }
    }

    public string DetailGlyph
    {
        get
        {
            return SelectedItem switch
            {
                WeaponViewModel => "\uE7AD",
                ArtifactViewModel => "\uECA5",
                FoodViewModel => "\uE8B7",
                _ => "\uE8FD"
            };
        }
    }

    public string TotalCollectedText => string.Format(
        "BackpackWindow_TotalItems".GetLocalized(),
        CatalogItemCount,
        OwnedItemCount);

    public int OwnedItemCount =>
        Weapons.Count(w => w.HasInstance)
        + Artifacts.Count(a => a.HasInstance)
        + MaterialGroups.Sum(g => g.Items.Count(i => i.CountValue > 0))
        + FoodGroups.Sum(g => g.Items.Count(i => i.CountValue > 0))
        + AssetGroups.Sum(g => g.Items.Count(i => i.CountValue > 0));

    public int CatalogItemCount =>
        Weapons.Count + Artifacts.Count
        + MaterialGroups.Sum(g => g.Items.Count)
        + FoodGroups.Sum(g => g.Items.Count)
        + AssetGroups.Sum(g => g.Items.Count);

    public string ProfileBadge
    {
        get
        {
            var path = GamePathDisplay;
            if (string.IsNullOrWhiteSpace(path) || path == BackpackLocalization.Get("GamePathFallback"))
                return "BackpackWindow_ProfileMissing".GetLocalized();
            return path.Length > 36
                ? "..." + path[^34..]
                : path;
        }
    }

    public string GameStatusLabel
    {
        get
        {
            if (!HasSelectedPath)
                return "BackpackWindow_StatusNoPath".GetLocalized();
            return IsGameRunning
                ? "BackpackWindow_StatusRunning".GetLocalized()
                : "BackpackWindow_StatusIdle".GetLocalized();
        }
    }

    public void CloseDetail() => SelectedItem = null;

    public void InitializeWindowCollections()
    {
        if (SidebarTabs.Count == 0)
        {
            SidebarTabs.Add(new(BackpackTab.Overview, "\uE80F", () => true));
            SidebarTabs.Add(new(BackpackTab.Weapons, "\uE7AD", () => Weapons.Count > 0));
            SidebarTabs.Add(new(BackpackTab.Artifacts, "\uECA5", () => true));
            SidebarTabs.Add(new(BackpackTab.Materials, "\uE8FD", () => MaterialGroups.Count > 0));
            SidebarTabs.Add(new(BackpackTab.Food, "\uE8B7", () => FoodGroups.Count > 0));
            SidebarTabs.Add(new(BackpackTab.Gadgets, "\uE950", () => true));
            SidebarTabs.Add(new(BackpackTab.Assets, "\uE734", () => AssetGroups.Count > 0));
        }

        if (SummaryStats.Count == 0)
        {
            SummaryStats.Add(new(this, BackpackStatKind.Weapon, "\uE7AD"));
            SummaryStats.Add(new(this, BackpackStatKind.Artifact, "\uECA5"));
            SummaryStats.Add(new(this, BackpackStatKind.Food, "\uE8B7"));
            SummaryStats.Add(new(this, BackpackStatKind.Asset, "\uE734"));
        }

        SelectedTab = CurrentTab;
    }
}

public sealed partial class BackpackSidebarEntry : ObservableObject
{
    public BackpackSidebarEntry(BackpackTab tab, string glyph, Func<bool> hasData)
    {
        Tab = tab;
        Glyph = glyph;
        HasData = hasData;
    }

    public BackpackTab Tab { get; }
    public string Glyph { get; }
    public Func<bool> HasData { get; }
    public string CountLabel { get; set; } = string.Empty;

    public string Label => Tab switch
    {
        BackpackTab.Overview  => "Backpack_TabOverview".GetLocalized(),
        BackpackTab.Weapons   => "Backpack_TabWeapon.Header".GetLocalized(),
        BackpackTab.Artifacts => "Backpack_TabArtifact.Header".GetLocalized(),
        BackpackTab.Materials => "Backpack_TabMaterial.Header".GetLocalized(),
        BackpackTab.Food      => "Backpack_TabFood.Header".GetLocalized(),
        BackpackTab.Gadgets   => "Backpack_TabGadget.Header".GetLocalized(),
        BackpackTab.Assets    => "Backpack_TabAsset.Header".GetLocalized(),
        _ => Tab.ToString()
    };
}

public enum BackpackStatKind { Weapon, Artifact, Food, Asset }

public sealed class BackpackSummaryStat
{
    private readonly BackpackViewModel _owner;

    public BackpackSummaryStat(BackpackViewModel owner, BackpackStatKind kind, string glyph)
    {
        _owner = owner;
        Kind = kind;
        Glyph = glyph;
    }

    public BackpackStatKind Kind { get; }
    public string Glyph { get; }
    public string ColorTag { get; } = "accent";

    public string Value => Kind switch
    {
        BackpackStatKind.Weapon   => _owner.OwnedWeaponCount.ToString("N0"),
        BackpackStatKind.Artifact => _owner.LockedArtifactCount.ToString("N0"),
        BackpackStatKind.Food     => _owner.CookableFoodCount.ToString("N0"),
        BackpackStatKind.Asset    => _owner.OwnedMaterialCount.ToString("N0"),
        _ => "0"
    };

    public string Label => Kind switch
    {
        BackpackStatKind.Weapon   => "Backpack_SummaryWeaponLabel".GetLocalized(),
        BackpackStatKind.Artifact => "Backpack_SummaryArtifactLabel".GetLocalized(),
        BackpackStatKind.Food     => "Backpack_SummaryFoodLabel".GetLocalized(),
        BackpackStatKind.Asset    => "Backpack_SummaryAssetLabel".GetLocalized(),
        _ => string.Empty
    };
}
