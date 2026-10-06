/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;

namespace FufuLauncher.Views;

public sealed partial class PluginConfigPage : Page
{
    private static readonly TimeSpan ExitAnimationTimeout = TimeSpan.FromMilliseconds(600);
    private readonly ObservableCollection<PluginConfigOption> _visibleOptions = new();
    private readonly ObservableCollection<PluginConfigInfoItem> _infoItems = new();
    private readonly DispatcherQueueTimer _autoSaveTimer;
    private PluginItem? _pluginItem;
    private PluginConfiguration? _configuration;
    private string? _loadedConfigPath;
    private CancellationTokenSource? _loadCancellation;
    private Task<bool>? _saveTask;
    private TaskCompletionSource<bool>? _exitAnimationCompletion;
    private Func<Task>? _retryAction;
    private bool _isActive;
    private bool _isLoading;
    private bool _isInitialized;
    private bool _isNavigatingBack;
    private bool _isRetrying;
    private bool _hasSaveError;

    public PluginConfigPage()
    {
        InitializeComponent();
        ConfigGridView.ItemsSource = _visibleOptions;
        InfoItemsControl.ItemsSource = _infoItems;
        _autoSaveTimer = DispatcherQueue.CreateTimer();
        _autoSaveTimer.Interval = TimeSpan.FromMilliseconds(350);
        _autoSaveTimer.IsRepeating = false;
        _autoSaveTimer.Tick += OnAutoSaveTimerTick;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        _isActive = ReferenceEquals(Frame?.Content, this);
        RestorePageInteraction();
        EntranceStoryboard.Begin();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        _exitAnimationCompletion?.TrySetResult(false);
        EntranceStoryboard.Stop();
        ExitStoryboard.Stop();
        RootLayoutGrid.IsHitTestVisible = true;
    }

    private void UpdateBackButtonState()
    {
        BackButton.IsEnabled = _isActive && !_isNavigatingBack && Frame != null;
    }

    private bool IsCurrentPage(Frame frame)
    {
        return _isActive && IsLoaded && ReferenceEquals(frame.Content, this);
    }

    private void RestorePageInteraction()
    {
        EntranceStoryboard.Stop();
        ExitStoryboard.Stop();
        RootLayoutGrid.Opacity = 1;
        PageTranslation.Y = 0;
        RootLayoutGrid.IsHitTestVisible = true;
        UpdateBackButtonState();
    }

    private void OnExitStoryboardCompleted(object sender, object e)
    {
        _exitAnimationCompletion?.TrySetResult(true);
    }

    private async Task<bool> PlayExitAnimationAsync()
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _exitAnimationCompletion = completion;
        try
        {
            var opacity = RootLayoutGrid.Opacity;
            var offset = PageTranslation.Y;
            EntranceStoryboard.Stop();
            ExitStoryboard.Stop();
            RootLayoutGrid.Opacity = opacity;
            PageTranslation.Y = offset;
            RootLayoutGrid.IsHitTestVisible = false;
            ExitStoryboard.Begin();
            return await completion.Task.WaitAsync(ExitAnimationTimeout);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginConfig] Exit animation interrupted: {ex}");
            return _isActive && IsLoaded;
        }
        finally
        {
            if (ReferenceEquals(_exitAnimationCompletion, completion))
            {
                _exitAnimationCompletion = null;
            }
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isActive = true;
        UpdateBackButtonState();

        if (e.Parameter is PluginItem item)
        {
            _pluginItem = item;
            UpdatePluginHeader();
            await LoadConfigAsync();
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isActive = false;
        UpdateBackButtonState();
        _exitAnimationCompletion?.TrySetResult(false);
        _loadCancellation?.Cancel();
        _autoSaveTimer.Stop();
        _ = SavePendingChangesAsync();
        base.OnNavigatedFrom(e);
    }

    private async Task LoadConfigAsync()
    {
        _loadCancellation?.Cancel();
        _loadCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        _isLoading = true;
        _isInitialized = false;
        _autoSaveTimer.Stop();

        if (_configuration != null)
        {
            foreach (var option in _configuration.Options)
            {
                option.PropertyChanged -= OnOptionPropertyChanged;
            }
        }

        _configuration = null;
        _loadedConfigPath = null;
        _saveTask = null;
        _retryAction = null;
        _hasSaveError = false;
        _visibleOptions.Clear();
        _infoItems.Clear();
        ConfigMessageBar.IsOpen = false;
        SettingsSearchBox.IsEnabled = false;
        ReloadButton.IsEnabled = false;
        SaveStatusPanel.Visibility = Visibility.Collapsed;
        OptionCountTextBlock.Text = "PluginConfigPage_Title".GetLocalized();
        SettingsScrollViewer.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Collapsed;
        LoadingPanel.Visibility = Visibility.Visible;
        LoadingRing.IsActive = true;

        try
        {
            var path = _pluginItem?.ConfigFilePath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException(string.Format(
                    CultureInfo.CurrentCulture, "PluginConfigPage_MissingFile".GetLocalized(), path));
            }

            var configuration = await PluginConfiguration.LoadAsync(path, cancellation.Token);
            if (cancellation.IsCancellationRequested || !_isActive)
            {
                return;
            }

            _configuration = configuration;
            _loadedConfigPath = path;
            foreach (var option in configuration.Options)
            {
                option.PropertyChanged += OnOptionPropertyChanged;
            }

            foreach (var item in configuration.GeneralInfo)
            {
                _infoItems.Add(new PluginConfigInfoItem(item.Key, item.Value));
            }

            UpdatePluginHeader(configuration.GeneralInfo);
            SetSaveStatus("PluginConfigPage_AutoSaveHint".GetLocalized(), "\uE73E");
            SaveStatusPanel.Visibility = configuration.Options.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateVisibleOptions();
            _isInitialized = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginConfig] Load failed: {ex}");
            if (!cancellation.IsCancellationRequested && _isActive)
            {
                ShowError("PluginConfigPage_LoadFailed".GetLocalized(),
                    string.Format(CultureInfo.CurrentCulture, "PluginConfigPage_LoadFailedMessage".GetLocalized(), ex.Message),
                    LoadConfigAsync);
                ShowEmptyState("PluginConfigPage_LoadFailed", "PluginConfigPage_LoadFailedHint", "\uE783", false);
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation))
            {
                _isLoading = false;
                LoadingRing.IsActive = false;
                LoadingPanel.Visibility = Visibility.Collapsed;
                SettingsSearchBox.IsEnabled = _configuration?.Options.Count > 0;
                ReloadButton.IsEnabled = true;
            }
        }
    }

    private void UpdatePluginHeader(IReadOnlyDictionary<string, string>? generalInfo = null)
    {
        if (_pluginItem == null)
        {
            return;
        }

        string GetInfo(string key, string fallback)
        {
            return generalInfo != null && generalInfo.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
        }

        TitleTextBlock.Text = GetInfo("Name", _pluginItem.DisplayName);
        ToolTipService.SetToolTip(TitleTextBlock, TitleTextBlock.Text);
        DescriptionTextBlock.Text = GetInfo("Description", _pluginItem.Description);
        if (string.IsNullOrWhiteSpace(DescriptionTextBlock.Text))
        {
            DescriptionTextBlock.Text = "PluginConfigPage_AdjustParams".GetLocalized();
        }

        ToolTipService.SetToolTip(DescriptionTextBlock, DescriptionTextBlock.Text);
        DeveloperTextBlock.Text = GetInfo("Developer", _pluginItem.Developer);
        DeveloperTextBlock.Visibility = string.IsNullOrWhiteSpace(DeveloperTextBlock.Text) ? Visibility.Collapsed : Visibility.Visible;
        ToolTipService.SetToolTip(DeveloperTextBlock, DeveloperTextBlock.Text);
        ModifiedDateTextBlock.Text = _pluginItem.DateModified.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
        ConfigPathTextBlock.Text = _pluginItem.ConfigFilePath ?? string.Empty;
    }

    private void OnSearchTextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (_configuration != null && !_isLoading)
        {
            UpdateVisibleOptions();
        }
    }

    private void UpdateVisibleOptions()
    {
        if (_configuration == null)
        {
            return;
        }

        var query = SettingsSearchBox.Text.Trim();
        var options = _configuration.Options.Where(option => query.Length == 0 ||
            option.DisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            option.SectionName.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
            option.Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)).ToArray();

        _visibleOptions.Clear();
        foreach (var option in options)
        {
            _visibleOptions.Add(option);
        }

        OptionCountTextBlock.Text = query.Length == 0
            ? string.Format(CultureInfo.CurrentCulture, "PluginConfigPage_OptionCount".GetLocalized(), options.Length)
            : string.Format(CultureInfo.CurrentCulture, "PluginConfigPage_FilteredOptionCount".GetLocalized(), options.Length, _configuration.Options.Count);
        SettingsScrollViewer.ChangeView(null, 0, null, true);
        SettingsScrollViewer.Visibility = options.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyStatePanel.Visibility = options.Length > 0 ? Visibility.Collapsed : Visibility.Visible;

        if (options.Length == 0)
        {
            var hasOptions = _configuration.Options.Count > 0;
            ShowEmptyState(hasOptions ? "PluginConfigPage_NoResults" : "PluginConfigPage_NoOptions",
                hasOptions ? "PluginConfigPage_NoResultsHint" : "PluginConfigPage_NoOptionsHint",
                hasOptions ? "\uE721" : "\uE9CE", hasOptions);
        }
    }

    private void ShowEmptyState(string titleKey, string hintKey, string glyph, bool canClearSearch)
    {
        SettingsScrollViewer.Visibility = Visibility.Collapsed;
        EmptyStatePanel.Visibility = Visibility.Visible;
        EmptyStateIcon.Glyph = glyph;
        EmptyStateTitleTextBlock.Text = titleKey.GetLocalized();
        EmptyStateHintTextBlock.Text = hintKey.GetLocalized();
        ClearSearchButton.Visibility = canClearSearch ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnClearSearchClick(object sender, RoutedEventArgs e)
    {
        SettingsSearchBox.Text = string.Empty;
        SettingsSearchBox.Focus(FocusState.Programmatic);
    }

    private void OnTextOptionChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitialized && sender is TextBox { Tag: PluginConfigOption option } textBox)
        {
            option.Value = textBox.Text;
        }
    }

    private void OnOptionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_isInitialized || e.PropertyName != nameof(PluginConfigOption.Value))
        {
            return;
        }

        if (!_isActive)
        {
            _ = SavePendingChangesAsync();
            return;
        }

        SetSaveStatus("PluginConfigPage_Saving".GetLocalized(), "\uE895");
        _autoSaveTimer.Stop();
        _autoSaveTimer.Start();
    }

    private async void OnAutoSaveTimerTick(DispatcherQueueTimer sender, object args)
    {
        await SavePendingChangesAsync();
    }

    private Task<bool> SavePendingChangesAsync()
    {
        _autoSaveTimer.Stop();
        if (_configuration == null || _loadedConfigPath == null)
        {
            return Task.FromResult(true);
        }

        if (_saveTask is { IsCompleted: false })
        {
            return _saveTask;
        }

        return _saveTask = SaveConfigurationAsync(_configuration, _loadedConfigPath);
    }

    private async Task<bool> SaveConfigurationAsync(PluginConfiguration configuration, string path)
    {
        try
        {
            if (configuration.HasChanges)
            {
                if (_isActive)
                {
                    SetSaveStatus("PluginConfigPage_Saving".GetLocalized(), "\uE895");
                }
                await configuration.SaveAsync(path);
            }

            if (_isActive && ReferenceEquals(_configuration, configuration))
            {
                SetSaveStatus("DeviceInfo_Saved".GetLocalized(), "\uE73E");
                if (_hasSaveError)
                {
                    ConfigMessageBar.IsOpen = false;
                    _hasSaveError = false;
                }
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginConfig] Save failed: {ex}");
            if (_isActive && ReferenceEquals(_configuration, configuration))
            {
                SetSaveStatus("PluginConfigPage_SaveFailed".GetLocalized(), "\uE783");
                ShowError("PluginConfigPage_SaveFailed".GetLocalized(),
                    string.Format(CultureInfo.CurrentCulture, "PluginConfigPage_SaveFailedMessage".GetLocalized(), ex.Message),
                    RetrySaveAsync);
                _hasSaveError = true;
            }
            return false;
        }
    }

    private void SetSaveStatus(string text, string glyph)
    {
        SaveStatusTextBlock.Text = text;
        SaveStatusIcon.Glyph = glyph;
    }

    private void ShowError(string title, string message, Func<Task> retryAction)
    {
        _retryAction = retryAction;
        _hasSaveError = false;
        ConfigMessageBar.Title = title;
        ConfigMessageBar.Message = message;
        ConfigMessageBar.IsOpen = true;
    }

    private async Task RetrySaveAsync()
    {
        await SavePendingChangesAsync();
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _isRetrying || _retryAction == null)
        {
            return;
        }

        _isRetrying = true;
        try
        {
            await _retryAction();
        }
        finally
        {
            _isRetrying = false;
        }
    }

    private async void OnReloadClick(object sender, RoutedEventArgs e)
    {
        if (_isLoading || _isNavigatingBack)
        {
            return;
        }

        ReloadButton.Focus(FocusState.Programmatic);
        if (await SavePendingChangesAsync() && _isActive)
        {
            await LoadConfigAsync();
        }
    }

    private async void OnBackClick(object sender, RoutedEventArgs e)
    {
        await NavigateBackAsync();
    }

    private async Task NavigateBackAsync()
    {
        var frame = Frame;
        if (_isNavigatingBack || frame == null || !IsCurrentPage(frame))
        {
            return;
        }

        _isNavigatingBack = true;
        BackButton.Focus(FocusState.Programmatic);
        UpdateBackButtonState();
        try
        {
            if (!await SavePendingChangesAsync() || !IsCurrentPage(frame))
            {
                return;
            }

            if (!await PlayExitAnimationAsync() || !IsCurrentPage(frame))
            {
                return;
            }

            while (frame.BackStack.Count > 0 &&
                   frame.BackStack[frame.BackStack.Count - 1].SourcePageType == typeof(PluginConfigPage))
            {
                frame.BackStack.RemoveAt(frame.BackStack.Count - 1);
            }

            var transition = new SuppressNavigationTransitionInfo();
            if (frame.CanGoBack)
            {
                frame.GoBack(transition);
            }
            else
            {
                frame.NavigateToType(typeof(PluginPage), null, new FrameNavigationOptions
                {
                    IsNavigationStackEnabled = false,
                    TransitionInfoOverride = transition
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginConfig] Back navigation failed: {ex}");
            if (IsCurrentPage(frame))
            {
                ShowError("GameAnnouncement_Back".GetLocalized(), ex.Message, NavigateBackAsync);
            }
        }
        finally
        {
            _isNavigatingBack = false;
            if (IsCurrentPage(frame))
            {
                RestorePageInteraction();
            }
            UpdateBackButtonState();
        }
    }

    private async void OnOpenFolderClick(object sender, RoutedEventArgs e)
    {
        await OpenPluginFolderAsync();
    }

    private async Task OpenPluginFolderAsync()
    {
        if (_pluginItem == null)
        {
            return;
        }

        try
        {
            var folder = await StorageFolder.GetFolderFromPathAsync(_pluginItem.DirectoryPath);
            await Windows.System.Launcher.LaunchFolderAsync(folder);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginConfig] Open folder failed: {ex}");
            if (_isActive)
            {
                ShowError("OpenFolderLabel".GetLocalized(), ex.Message, OpenPluginFolderAsync);
            }
        }
    }
}