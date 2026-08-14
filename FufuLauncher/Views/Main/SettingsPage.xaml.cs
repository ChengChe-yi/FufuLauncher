/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage : Page
{
    #region 字段

    private Window _easterEggWindow;

    private bool _cpuUsageWarningToggleLoaded;

    private bool _isNavigatingFromMenu;
    private DispatcherTimer? _navLockTimer;

    private static readonly string[] _sectionTags =
        { "AppearanceItem", "HomeCardsItem", "WidgetsItem", "NotesItem", "HomeTextItem",
          "BackgroundItem", "WindowEffectsItem",
          "LaunchConfigItem", "ScreenshotSettingsItem", "CheckinSettingsItem",
          "LanguageItem", "WindowBehaviorItem", "StartupSoundItem", "AdvancedOptionsItem", "UpdateItem",
          "AboutItem", "SecurityAuthItem" };

    private readonly List<SettingsSearchResult> _searchIndex = new();
    private FrameworkElement? _highlightedRow;
    private Microsoft.UI.Xaml.Media.Brush? _highlightedRowOriginalBackground;
    private DispatcherTimer? _highlightTimer;

    private bool _isRecordingHotkey;

    private bool _injectionModuleLoaded = false;

    #endregion

    #region 初始化与页面生命周期

    public SettingsViewModel ViewModel
    {
        get;
    }

    public SettingsPage()
    {
        ViewModel = App.GetService<SettingsViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    protected async override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (ViewModel != null)
        {
            await ViewModel.ReloadSettingsAsync();
        }

        await LoadInjectionModuleSelectionAsync();
        await UpdateApplyPredownloadRowVisibilityAsync();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        EntranceStoryboard.Begin();

        _isNavigatingFromMenu = true;
        if (SettingsNavigationView.SelectedItem == null)
        {
            SettingsNavigationView.SelectedItem = SettingsNavigationView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
        }
        _isNavigatingFromMenu = false;
    }

    #endregion

    #region 对话框辅助

    private async Task<ContentDialogResult> ShowSafeDialogAsync(ContentDialog dialog)
    {
        try
        {
            return await dialog.ShowAsync();
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            await Task.Delay(300);
            try
            {
                return await dialog.ShowAsync();
            }
            catch
            {
                return ContentDialogResult.None;
            }
        }
    }

    #endregion

    #region 注入模块选择

    private async Task LoadInjectionModuleSelectionAsync()
    {
        try
        {
            var settingsService = App.GetService<FufuLauncher.Contracts.Services.ILocalSettingsService>();
            var saved = await settingsService.ReadSettingAsync("InjectionModule");
            var moduleId = saved?.ToString() ?? "DLL";

            for (int i = 0; i < InjectionModuleComboBox.Items.Count; i++)
            {
                if (InjectionModuleComboBox.Items[i] is ComboBoxItem item && item.Tag?.ToString() == moduleId)
                {
                    InjectionModuleComboBox.SelectedIndex = i;
                    break;
                }
            }
        }
        catch { }
    }

    private async void InjectionModuleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_injectionModuleLoaded)
        {
            _injectionModuleLoaded = true;
            return;
        }

        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem selectedItem)
        {
            var moduleId = selectedItem.Tag?.ToString() ?? "DLL";
            var settingsService = App.GetService<FufuLauncher.Contracts.Services.ILocalSettingsService>();
            await settingsService.SaveSettingAsync("InjectionModule", moduleId);
        }
    }

    #endregion
}
