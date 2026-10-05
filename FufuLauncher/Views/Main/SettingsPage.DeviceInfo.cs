/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    #region 设备信息（仅开发者）

    private void OnOpenDeviceInfoWindowClick(object sender, RoutedEventArgs e)
    {
        _ = OpenDeviceInfoWindowAsync();
    }

    private async Task OpenDeviceInfoWindowAsync()
    {
        if (!await HasDeveloperAccessAsync())
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

        var deviceInfoWindow = new DeviceInfoWindow();
        deviceInfoWindow.Activate();
    }

    private async Task UpdateDeviceInfoRowVisibilityAsync()
    {
        var visibility = await IsDevFeaturesEnabledAsync() ? Visibility.Visible : Visibility.Collapsed;
        DeviceInfoRow.Visibility = visibility;
        DeviceInfoRowDivider.Visibility = visibility;
    }

    private static async Task<bool> IsDevFeaturesEnabledAsync()
    {
        var localSettings = App.GetService<ILocalSettingsService>();
        if (localSettings is null)
        {
            return false;
        }

        var value = await localSettings.ReadSettingAsync("IsDevFeaturesEnabled");
        return value is not null && Convert.ToBoolean(value);
    }

    private static async Task<bool> HasDeveloperAccessAsync()
    {
        if (!await IsDevFeaturesEnabledAsync())
        {
            return false;
        }

        var authorizationService = App.GetService<DeveloperAuthorizationService>();
        return authorizationService is not null && await authorizationService.IsAuthorizedAsync();
    }

    #endregion
}
