/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using FufuLauncher.Models.GameServer;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    #region 预下载

    private async void OnApplyPredownloadClick(object sender, RoutedEventArgs e)
    {
        string? gameDir = null;
        var configService = App.GetService<IGameConfigService>();
        if (configService is not null)
        {
            gameDir = await configService.GetSavedGamePathAsync();
        }

        if (string.IsNullOrEmpty(gameDir))
        {
            await ShowSafeDialogAsync(new ContentDialog
            {
                Title = "ErrorTitle".GetLocalized(),
                Content = "Err_GamePathNotFound".GetLocalized(),
                CloseButtonText = "OkBtn".GetLocalized(),
                XamlRoot = XamlRoot
            });
            return;
        }

        if (File.Exists(gameDir))
        {
            gameDir = Path.GetDirectoryName(gameDir) ?? gameDir;
        }

        bool devEnabled = false;
        var localSettings = App.GetService<FufuLauncher.Contracts.Services.ILocalSettingsService>();
        if (localSettings is not null)
        {
            var value = await localSettings.ReadSettingAsync("IsDevFeaturesEnabled");
            devEnabled = value is not null && Convert.ToBoolean(value);
        }

        var authorizationService = App.GetService<DeveloperAuthorizationService>();
        if (!devEnabled || authorizationService is null || !await authorizationService.IsAuthorizedAsync())
        {
            await ShowSafeDialogAsync(new ContentDialog
            {
                Title = "ErrorTitle".GetLocalized(),
                Content = "GameUpdate_DevOnly".GetLocalized(),
                CloseButtonText = "OkBtn".GetLocalized(),
                XamlRoot = XamlRoot
            });
            return;
        }

        var newWindow = new GameUpdateWindow(gameDir, GameUpdateOperationKind.ApplyPredownload);
        newWindow.Activate();
    }

    private async Task UpdateApplyPredownloadRowVisibilityAsync()
    {
        bool devEnabled = false;
        var localSettings = App.GetService<FufuLauncher.Contracts.Services.ILocalSettingsService>();
        if (localSettings is not null)
        {
            var value = await localSettings.ReadSettingAsync("IsDevFeaturesEnabled");
            devEnabled = value is not null && Convert.ToBoolean(value);
        }

        ApplyPredownloadRow.Visibility = devEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    #endregion
}
