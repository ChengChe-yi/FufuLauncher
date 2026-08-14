/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Models.GameAnnouncement;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 游戏公告

    [ObservableProperty]
    private AnnouncementViewMode _announcementViewMode = AnnouncementViewMode.New;

    partial void OnAnnouncementViewModeChanged(AnnouncementViewMode value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync(LocalSettingsService.AnnouncementViewModeKey, value.ToString());
    }

    #endregion
}
