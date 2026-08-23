/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models.Backpack;
using FufuLauncher.Services;
using FufuLauncher.Services.Backpack;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;

namespace FufuLauncher.Views;

public sealed partial class BackpackWindow : Window
{
    private readonly BackpackRuntimeService _runtime;
    private bool _subscribed;

    public BackpackViewModel ViewModel => _runtime.ViewModel;

    public BackpackWindow()
    {
        _runtime = App.GetService<BackpackRuntimeService>();
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        Activated += OnActivated;
        Closed += OnClosed;
    }

    private async void OnActivated(object sender, WindowActivatedEventArgs e)
    {
        Activated -= OnActivated;

        if (!_subscribed)
        {
            _runtime.DataReceived += OnDataReceived;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribed = true;
        }

        await _runtime.InitializeAsync();
        ViewModel.RefreshBrowse();
        ViewModel.InitializeWindowCollections();
        ViewModel.Dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, ViewModel.RebuildOverview);
        PlayEntranceAnimation();
    }

    private void OnClosed(object sender, WindowEventArgs e)
    {
        if (!_subscribed) return;
        _runtime.DataReceived -= OnDataReceived;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribed = false;
    }

    private void PlayEntranceAnimation()
    {
        if (MainContent.RenderTransform is not TranslateTransform t) return;
        t.Y = 18;
        var opacity = MainContent.Opacity;

        var sb = new Storyboard();
        var tAnim = new DoubleAnimation
        {
            From = 18,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(360)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(tAnim, t);
        Storyboard.SetTargetProperty(tAnim, "Y");

        var oAnim = new DoubleAnimation
        {
            From = 0,
            To = opacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(260))
        };
        Storyboard.SetTarget(oAnim, MainContent);
        Storyboard.SetTargetProperty(oAnim, "Opacity");

        sb.Children.Add(tAnim);
        sb.Children.Add(oAnim);
        sb.Begin();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BackpackViewModel.HasSelectedPath)
            or nameof(BackpackViewModel.IsInitializing))
        {
            ViewModel.RefreshBrowse();
            ViewModel.RebuildOverview();
        }
    }

    private void OnDataReceived() => ViewModel.RebuildOverview();

    private void OnSidebarItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BackpackSidebarEntry entry)
        {
            ViewModel.SelectedItem = null;
            ViewModel.SelectedTab = entry.Tab;
        }
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        ViewModel.SelectedItem = e.ClickedItem;
        if (MainContent.Resources.TryGetValue("DetailSlideInStoryboard", out var obj) && obj is Storyboard sb)
        {
            DetailTransform.X = 380;
            DetailPane.Opacity = 0;
            sb.Begin();
        }
    }

    private void OnCloseDetailClick(object sender, RoutedEventArgs e) => ViewModel.CloseDetail();

    private void OnToggleDetailPane(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedItem is null)
        {
            ViewModel.SelectedItem = ViewModel.DisplayWeapons.FirstOrDefault()
                ?? (object?)ViewModel.DisplayArtifacts.FirstOrDefault();
        }
        else
        {
            ViewModel.CloseDetail();
        }
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange) return;
        ViewModel.SetSearch(sender.Text);
    }

    private void OnSubcategoryChipClick(object sender, RoutedEventArgs e)
        => ViewModel.SetSubcategory((sender as ToggleButton)?.Tag as BackpackBrowseChip);

    private void OnFilterChipClick(object sender, RoutedEventArgs e)
        => ViewModel.SetFilter((sender as ToggleButton)?.Tag as BackpackBrowseChip);

    private void OnSortChipClick(object sender, RoutedEventArgs e)
        => ViewModel.SetSort((sender as ToggleButton)?.Tag as BackpackBrowseChip);

    private void OnResetBrowse(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetBrowse();
    }

    private async void OnImportBackpackClick(object sender, RoutedEventArgs e)
    {
        var path = await FilePickerService.PickOpenFileAsync(
            this,
            new List<(string Label, string[] Extensions)> { ("背包数据", new[] { ".json" }) },
            Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            msg => ShowDialogAsync("BackpackWindow_ImportBackpack".GetLocalized(), msg));

        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var text = await File.ReadAllTextAsync(path, Encoding.UTF8);
            var snap = JsonSerializer.Deserialize<BackpackExportSnapshot>(text);
            if (snap is null)
            {
                ShowDialogAsync("BackpackWindow_ImportBackpack".GetLocalized(), "无效的备份格式");
                return;
            }

            var db = App.GetService<BackpackDbService>();
            db.SaveWeapons(snap.Weapons);
            db.SaveArtifacts(snap.Artifacts);
            db.SaveMaterials(snap.MaterialCounts);
            db.SaveProps(snap.MaterialProps);

            ViewModel.RebuildOverview();
            ViewModel.RefreshBrowse();
            ShowDialogAsync("BackpackWindow_ImportBackpack".GetLocalized(), "数据已导入");
        }
        catch (Exception ex)
        {
            ShowDialogAsync("BackpackWindow_ImportBackpack".GetLocalized(), $"导入失败：{ex.Message}");
        }
    }

    private async void OnExportBackpackClick(object sender, RoutedEventArgs e)
    {
        var suggested = $"BackpackExport_{DateTime.Now:yyyyMMdd_HHmmss}";
        var path = await FilePickerService.PickSaveFileAsync(
            this,
            new List<(string Label, string[] Extensions)> { ("背包数据", new[] { ".json" }) },
            suggested,
            Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            msg => ShowDialogAsync("BackpackWindow_ExportBackpack".GetLocalized(), msg));

        if (string.IsNullOrEmpty(path)) return;

        try
        {
            var db = App.GetService<BackpackDbService>();
            var snap = new BackpackExportSnapshot
            {
                Weapons = db.LoadWeapons().ToList(),
                Artifacts = db.LoadArtifacts().ToList(),
                MaterialCounts = db.LoadMaterialCounts(),
                MaterialProps = db.LoadProps()
            };

            var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(snap, options), Encoding.UTF8);
            ShowDialogAsync("BackpackWindow_ExportBackpack".GetLocalized(), "数据已保存");
        }
        catch (Exception ex)
        {
            ShowDialogAsync("BackpackWindow_ExportBackpack".GetLocalized(), $"保存失败：{ex.Message}");
        }
    }

    private async void OnRefreshDataClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _runtime.InitializeAsync();
            ViewModel.RefreshBrowse();
            ViewModel.RebuildOverview();
        }
        catch (Exception ex)
        {
            ShowDialogAsync("BackpackWindow_RefreshData".GetLocalized(), ex.Message);
        }
    }

    private void OnViewGuideClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string? keyword = ViewModel.SelectedItem switch
            {
                WeaponViewModel w => w.Source.Name,
                ArtifactViewModel a => a.Source.SetName,
                FoodViewModel f => f.Name,
                SimpleItemViewModel s => s.Name,
                _ => null
            };
            if (string.IsNullOrEmpty(keyword)) return;
            var url = $"https://www.miyoushe.com/ys/search?keyword={Uri.EscapeDataString(keyword)}";
            Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Backpack] 打开攻略失败: {ex.Message}");
        }
    }

    private void ShowDialogAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = new TextBlock { Text = content, TextWrapping = TextWrapping.Wrap, MaxWidth = 400 },
            CloseButtonText = "OkBtn".GetLocalized(),
            XamlRoot = Content.XamlRoot
        };
        _ = dialog.ShowAsync();
    }
}

public sealed class BackpackExportSnapshot
{
    public List<WeaponEntry> Weapons { get; set; } = new();
    public List<ArtifactEntry> Artifacts { get; set; } = new();
    public Dictionary<uint, ulong> MaterialCounts { get; set; } = new();
    public Dictionary<uint, long> MaterialProps { get; set; } = new();
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
}
