/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Models;
using FufuLauncher.Services;
using FufuLauncher.Services.Background;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class MainPage
{
    #region 公告、令牌刷新与弹层选择

    private async void AnnouncementBell_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var announcementService = App.GetService<IAnnouncementService>();

            var announcementUrl = await announcementService.GetCurrentAnnouncementUrlAsync();

            if (string.IsNullOrEmpty(announcementUrl))
            {
                var localSettings = App.GetService<ILocalSettingsService>();

                var lastUrlObj = await localSettings.ReadSettingAsync("LastAnnouncementUrl");
                if (lastUrlObj is string lastUrl && !string.IsNullOrEmpty(lastUrl))
                {
                    announcementUrl = lastUrl;
                }
            }


            if (!string.IsNullOrEmpty(announcementUrl))
            {
                var announcementWindow = new AnnouncementWindowL(announcementUrl);
                announcementWindow.Activate();
            }
            else
            {
                Debug.WriteLine("[Announcement] 手动获取公告失败：未获取到且无本地缓存");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Announcement] 手动触发公告异常: {ex.Message}");
        }
    }

    private async void RefreshTokenButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var accountManager = App.GetService<AccountManager>();
            var activeId = accountManager.ActiveAccountId;
            if (activeId == null)
            {
                WeakReferenceMessenger.Default.Send(new NotificationMessage("Home_RefreshFailed".GetLocalized(), "Home_NoActiveAccount".GetLocalized(), NotificationType.Error));
                return;
            }

            var cookies = await accountManager.LoadCookiesAsync(activeId);
            if (cookies == null || cookies.Count == 0)
            {
                WeakReferenceMessenger.Default.Send(new NotificationMessage("Home_RefreshFailed".GetLocalized(), "Home_CannotLoadCredentials".GetLocalized(), NotificationType.Error));
                return;
            }

            var tokenService = new TokenRefreshService();
            var newCookies = await tokenService.RefreshCookieAsync(cookies, true);

            if (newCookies != null)
            {
                await accountManager.UpdateCookiesAsync(activeId, newCookies);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"手动刷新异常: {ex.Message}");
        }
    }

    private double GetIconOpacity(bool isEnabled)
    {
        return isEnabled ? 1.0 : 0.4;
    }

    private void BackgroundGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BackgroundUrlInfo info)
        {
            ViewModel.SelectSpecificBackgroundCommand.Execute(info);

            BackgroundFlyout.Hide();
        }
    }

    private void InjectionModuleListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is InjectionModuleInfo module)
        {
            ViewModel.SelectInjectionModuleCommand.Execute(module);

            InjectionModuleFlyout.Hide();
        }
    }

    #endregion
}
