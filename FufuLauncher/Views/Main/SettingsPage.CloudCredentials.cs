/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using WinRT.Interop;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    #region 云游戏凭证与 HDR

    private async void OnOpenHDRSettingsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new GenshinHDRLuminanceSettingDialog();
        dialog.XamlRoot = this.XamlRoot;
        await dialog.ShowAsync();
    }

    private void OnCloudCredentialClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string uid)
        {
            var cloudWindow = new CloudCredentialWindow(uid);
            cloudWindow.ExtendsContentIntoTitleBar = true;

            IntPtr hWnd = WindowNative.GetWindowHandle(cloudWindow);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico");
                if (File.Exists(iconPath))
                    appWindow.SetIcon(iconPath);

                var size = new Windows.Graphics.SizeInt32(1280, 720);
                appWindow.Resize(size);

                var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                if (displayArea != null)
                {
                    var centeredX = (displayArea.WorkArea.Width - size.Width) / 2;
                    var centeredY = (displayArea.WorkArea.Height - size.Height) / 2;
                    appWindow.Move(new Windows.Graphics.PointInt32(centeredX, centeredY));
                }
            }

            cloudWindow.Activate();
        }
    }

    private async void OnRemoveCloudCredentialClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string uid)
        {
            var dialog = new ContentDialog
            {
                Title = "RemoveCloudCredentialConfirmTitle".GetLocalized(),
                Content = "RemoveCloudCredentialConfirmContent".GetLocalized(),
                PrimaryButtonText = "DeleteLabel".GetLocalized(),
                CloseButtonText = "CancelBtn".GetLocalized(),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                await ViewModel.RemoveCloudCredentialAsync(uid);
            }
        }
    }

    #endregion
}
