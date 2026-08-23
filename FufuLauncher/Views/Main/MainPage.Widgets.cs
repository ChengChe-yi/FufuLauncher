/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FufuLauncher.Views;

public sealed partial class MainPage
{
    #region 小组件与快捷入口

    private readonly Dictionary<FrameworkElement, object> _cachedToolTips = new();

    private bool _isWidgetFlyoutEnabled = false;

    private async void OnWidgetSettingsClick(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mainWindow)
        {
            await mainWindow.NavigateToSettingsPageAsync();
        }
    }

    private void OnToggleWidgetFlyoutModeClick(object sender, RoutedEventArgs e)
    {
        _isWidgetFlyoutEnabled = !_isWidgetFlyoutEnabled;
        WidgetEyeIcon.Glyph = _isWidgetFlyoutEnabled ? "\uE8CB" : "\uE890";

        ToolTipService.SetToolTip(BtnWidgetGacha, _isWidgetFlyoutEnabled ? "Home_GachaTooltip".GetLocalized() : "Home_WidgetGacha".GetLocalized());
        ToolTipService.SetToolTip(BtnWidgetAchievement, _isWidgetFlyoutEnabled ? "Home_AchievementTooltip".GetLocalized() : "Home_WidgetAchievement".GetLocalized());
        ToolTipService.SetToolTip(BtnWidgetInventory, _isWidgetFlyoutEnabled ? "Home_InventoryTooltip".GetLocalized() : "Home_WidgetInventory".GetLocalized());
        ToolTipService.SetToolTip(BtnWidgetPlayerRole, _isWidgetFlyoutEnabled ? "Home_PlayerRoleTooltip".GetLocalized() : "Home_WidgetPlayerRole".GetLocalized());
        ToolTipService.SetToolTip(BtnWidgetDailyNote, _isWidgetFlyoutEnabled ? "Home_NotesTooltip".GetLocalized() : "Home_WidgetNotes".GetLocalized());
        ToolTipService.SetToolTip(BtnWidgetVideo, _isWidgetFlyoutEnabled ? "Home_VideoTooltip".GetLocalized() : "Home_WidgetVideo".GetLocalized());
        ToolTipService.SetToolTip(BtnWidgetBBS, _isWidgetFlyoutEnabled ? "Home_BBSTooltip".GetLocalized() : "Home_WidgetBBS".GetLocalized());
    }

    private void WidgetButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is FrameworkElement element)
        {
            if (_cachedToolTips.TryGetValue(element, out var cachedTooltip))
            {
                ToolTipService.SetToolTip(element, cachedTooltip);
                _cachedToolTips.Remove(element);
            }
        }
    }

    private async void OpenCheckinSettings_Click(object sender, RoutedEventArgs e)
    {
        if (App.MainWindow is MainWindow mainWindow)
        {
            await mainWindow.NavigateToSettingsPageAsync();
        }
    }

    private void OnOpenGachaAnalysisClick(object sender, RoutedEventArgs e)
    {
        var window = new GachaAnalysisWindow();
        window.Activate();
    }

    private void OnOpenAchievementsClick(object sender, RoutedEventArgs e)
    {
        var window = new AchievementWindow();
        window.Activate();
    }

    private void OnOpenInventoryClick(object sender, RoutedEventArgs e)
    {
        var window = new InventoryWindow();
        window.Activate();
    }

    private void OnOpenPlayerRolesClick(object sender, RoutedEventArgs e)
    {
        var window = new PlayerInfoWindow();
        window.Activate();
    }

    private void OnOpenDailyNoteClick(object sender, RoutedEventArgs e)
    {
        var window = new DailyNoteWindow();
        window.Activate();
    }

    private async void BBSButton_Click(object sender, RoutedEventArgs e)
    {
        ContentDialog riskDialog = new()
        {
            Title = "Home_BBSSecurityTitle".GetLocalized(),
            Content = "Home_BBSSecurityMessage".GetLocalized(),
            PrimaryButtonText = "Home_BBSConfirm".GetLocalized(),
            CloseButtonText = "CancelBtn".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await riskDialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            var bbsWindow = new BBSWindow();
            bbsWindow.Activate();
        }
    }

    private void OnOpenVideoResourcesClick(object sender, RoutedEventArgs e)
    {
        var window = new VideoResourcesWindow();
        window.Activate();
    }

    private void OpenCheckinCalendar_Click(object sender, RoutedEventArgs e)
    {
        var calendarWindow = new CheckinCalendarWindow();
        calendarWindow.Activate();
    }

    #endregion
}
