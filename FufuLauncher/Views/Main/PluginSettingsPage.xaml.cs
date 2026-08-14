/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FufuLauncher.ViewModels;
using FufuLauncher.Helpers;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage : Page
{
    public PluginSettingsViewModel ViewModel { get; }
    public MainViewModel MainVM { get; }
    public ControlPanelModel ControlPanelVM { get; }

    private bool _isInitializing = true;

    public PluginSettingsPage()
    {
        ViewModel = new PluginSettingsViewModel();
        MainVM = App.GetService<MainViewModel>();
        ControlPanelVM = App.GetService<ControlPanelModel>();
        InitializeComponent();
    
        Loaded += PluginSettingsPage_Loaded;
        Unloaded += PluginSettingsPage_Unloaded;
        
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private async void PluginSettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        EntranceStoryboard.Begin();
        StartMainPluginWatcher();
        ShowMainPluginMissingWarningIfNeeded();
        if (ViewModel.IsPluginCorrupted())
        {
            var dialog = new ContentDialog
            {
                Title = "Plugin_Corrupted_Title".GetLocalized(),
                Content = "Plugin_Corrupted_Content".GetLocalized(),
                PrimaryButtonText = "GotItBtn".GetLocalized(),
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }

        await VerifyFpsPluginHashAsync();
        
        await CheckAndShowFpsWarningAsync();
        
        if (ViewModel.SettingsOverlayVisibility == Visibility.Visible)
        {
            SettingsOverlay.Visibility = Visibility.Visible;
            SettingsOverlay.Opacity = 1;
        }
        
        _isInitializing = false;
    }

    private void PluginSettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _mainPluginWatcher?.Dispose();
        _mainPluginWatcher = null;
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
    
        if (e.Parameter is Models.PluginItem item)
        {
            var folderName = new DirectoryInfo(item.DirectoryPath).Name;
        
            if (folderName.Contains("FPS", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.SelectedPluginIndex = 1;
            }
            else if (folderName.Contains("Avatar", StringComparison.OrdinalIgnoreCase))
            {
                ViewModel.SelectedPluginIndex = 2;
            }
            else
            {
                ViewModel.SelectedPluginIndex = 0;
            }
        }
    }

    private async void ViewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isInitializing) return;

        if (e.PropertyName == nameof(ViewModel.SettingsOverlayVisibility))
        {
            if (ViewModel.SettingsOverlayVisibility == Visibility.Visible)
            {
                SettingsOverlay.Visibility = Visibility.Visible;
                OverlayFadeIn.Begin();
            }
            else
            {
                if (SettingsOverlay.Visibility == Visibility.Visible)
                {
                    OverlayFadeOut.Begin();
                }
            }
        }
        else if (e.PropertyName == nameof(ViewModel.SelectedPluginIndex))
        {
            await CheckAndShowFpsWarningAsync();
        }
        else if (e.PropertyName == nameof(ViewModel.IsFpsPluginEnabled))
        {
            if (!ViewModel.IsFpsPluginEnabled && !_isEnforcingFpsDisable)
            {
                await EnforceFpsPluginDisableAsync();
            }
        }
    }

    private void OverlayFadeOut_Completed(object sender, object e)
    {
        if (ViewModel.SettingsOverlayVisibility == Visibility.Collapsed)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }
    }

    public bool InvertBool(bool value) => !value;

    private void MoveDirectorySafe(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destDir, Path.GetFileName(file)), true);
        }
        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            MoveDirectorySafe(dir, Path.Combine(destDir, Path.GetFileName(dir)));
        }
        Directory.Delete(sourceDir, true);
    }
}
