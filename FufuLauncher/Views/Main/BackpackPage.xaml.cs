/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.ComponentModel;
using FufuLauncher.Helpers;
using FufuLauncher.Services.Backpack;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace FufuLauncher.Views;

public sealed partial class BackpackPage : Page
{
    private readonly BackpackRuntimeService _runtime;
    private ContentDialog? _syncDialog;
    private bool _subscribed;
    private bool _syncAttemptActive;
    private bool _syncReceivedData;

    public BackpackViewModel ViewModel => _runtime.ViewModel;

    public BackpackPage()
    {
        _runtime = App.GetService<BackpackRuntimeService>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        EntranceStoryboard.Begin();

        if (!_subscribed)
        {
            _runtime.DataReceived += OnDataReceived;
            ViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _subscribed = true;
        }

        await _runtime.InitializeAsync();
        ViewModel.RefreshBrowse();
        
        ViewModel.Dispatcher.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            ViewModel.RebuildOverview();
        });
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed) return;
        _runtime.DataReceived -= OnDataReceived;
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribed = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(BackpackViewModel.HasSelectedPath) or nameof(BackpackViewModel.IsInitializing))
        {
            ViewModel.RefreshBrowse();
            ViewModel.RebuildOverview();
        }
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || !int.TryParse(button.Tag?.ToString(), out var tabIndex)) return;
        button.IsChecked = true;
        ViewModel.SetTab((BackpackTab)tabIndex);
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange) return;
        ViewModel.SetSearch(sender.Text);
    }

    private void OnSubcategoryChipClick(object sender, RoutedEventArgs e) =>
        ViewModel.SetSubcategory((sender as ToggleButton)?.Tag as BackpackBrowseChip);

    private void OnFilterChipClick(object sender, RoutedEventArgs e) =>
        ViewModel.SetFilter((sender as ToggleButton)?.Tag as BackpackBrowseChip);

    private void OnSortChipClick(object sender, RoutedEventArgs e) =>
        ViewModel.SetSort((sender as ToggleButton)?.Tag as BackpackBrowseChip);

    private void OnResetBrowse(object sender, RoutedEventArgs e)
    {
        ViewModel.ResetBrowse();
        SearchBox.Text = string.Empty;
    }

    private void OnOpenGameSettings(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is global::FufuLauncher.MainWindow mainWindow)
            mainWindow.NavigateToPage("FufuLauncher.ViewModels.BlankViewModel");
    }

    private async void OnSyncBag(object sender, RoutedEventArgs e)
    {
        _syncReceivedData = false;
        _syncAttemptActive = true;

        try
        {
            await _runtime.LaunchAndSyncAsync();

            if (!_syncReceivedData)
            {
                _syncDialog = CreateSyncWaitingDialog();
                await _syncDialog.ShowAsync();
            }

            if (_syncReceivedData)
                await ShowMessageDialogAsync(BackpackLocalization.Get("NotifyImportTitle"), BackpackLocalization.Get("NotifyImportBody"));
        }
        catch (FileNotFoundException)
        {
            await ShowDetailDialogAsync(BackpackLocalization.Get("ModuleMissingTitle"), "ModuleNoticeTemplate", new object(), 520, "DialogConfirm");
        }
        catch (Exception ex)
        {
            ViewModel.StatusText = ex.Message;
            await ShowMessageDialogAsync(BackpackLocalization.Get("NotifyFailedTitle"), ex.Message);
        }
        finally
        {
            _syncDialog = null;
            _syncAttemptActive = false;
            ViewModel.IsLaunching = false;
            _runtime.KillLaunchedGame();
        }
    }

    private ContentDialog CreateSyncWaitingDialog() => new()
    {
        XamlRoot = XamlRoot,
        Title = BackpackLocalization.Get("SyncBagDialogTitle"),
        CloseButtonText = BackpackLocalization.Get("SyncBagDialogCancel"),
        DefaultButton = ContentDialogButton.None,
        Content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            Children =
            {
                new ProgressRing { IsActive = true, Width = 24, Height = 24 },
                new TextBlock { Text = BackpackLocalization.Get("SyncBagDialogWaiting"), VerticalAlignment = VerticalAlignment.Center }
            }
        }
    };

    private async Task ShowMessageDialogAsync(string title, string content)
    {
        if (XamlRoot is null) return;

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            CloseButtonText = BackpackLocalization.Get("DialogConfirm"),
            DefaultButton = ContentDialogButton.Close
        };
        await dialog.ShowAsync();
    }

    private void OnDataReceived()
    {
        if (_syncAttemptActive)
        {
            _syncReceivedData = true;
            _syncDialog?.Hide();
        }

        ViewModel.RebuildOverview();
    }

    private void OnKillGame(object sender, RoutedEventArgs e) => _runtime.KillLaunchedGame();

    private async void OnModuleDetails(object sender, RoutedEventArgs e)
    {
        await ShowDetailDialogAsync(BackpackLocalization.Get("ModuleNoticeTitle"), "ModuleDetailsTemplate", new object(), 560, "DialogConfirm");
    }

    private void OnPreviousPageClick(object sender, RoutedEventArgs e) => ViewModel.PreviousPage();
    private void OnNextPageClick(object sender, RoutedEventArgs e) => ViewModel.NextPage();

    private async void OnInventoryItemClick(object sender, ItemClickEventArgs e)
    {
        switch (e.ClickedItem)
        {
            case WeaponViewModel weapon:
                await ShowDetailDialogAsync(weapon.Source.Name, "WeaponDetailTemplate", weapon, 900);
                break;
            case ArtifactViewModel artifact:
                await ShowDetailDialogAsync(artifact.Source.SetName, "ArtifactDetailTemplate", artifact, 900);
                break;
            case FoodViewModel food:
                await ShowDetailDialogAsync(food.Name, "FoodDetailTemplate", food, 900);
                break;
            case SimpleItemViewModel item:
                await ShowDetailDialogAsync(item.Name, "SimpleItemDetailTemplate", item, 520);
                break;
        }
    }

    private async Task ShowDetailDialogAsync(string title, string templateKey, object content, double maxWidth, string? closeTextKey = null)
    {
        if (XamlRoot is null || Resources[templateKey] is not DataTemplate template) return;

        var host = new ContentControl
        {
            ContentTemplate = template,
            Content = content,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch
        };

        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = host,
            CloseButtonText = BackpackLocalization.Get(closeTextKey ?? "DialogClose"),
            DefaultButton = ContentDialogButton.Close
        };
        dialog.Resources["ContentDialogMaxWidth"] = maxWidth;
        await dialog.ShowAsync();
    }
}
