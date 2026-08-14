/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;
using File = System.IO.File;

namespace FufuLauncher.Views;

public sealed partial class BlankPage
{
    #region 操作

    private async void VerifyGame_Click(object sender, RoutedEventArgs e)
    {
        if (_currentConfig == null || string.IsNullOrEmpty(_currentConfig.GamePath))
        {
            await ShowError("Err_GamePathNotFound".GetLocalized());
            return;
        }

        string gameDir = _currentConfig.GamePath;
        if (File.Exists(gameDir))
        {
            gameDir = Path.GetDirectoryName(gameDir) ?? gameDir;
        }

        var newWindow = new Window();
        newWindow.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        newWindow.ExtendsContentIntoTitleBar = true;
        newWindow.Title = "Title_VerifyGameIntegrity".GetLocalized();

        var hWnd = WindowNative.GetWindowHandle(newWindow);
        var winId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(winId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(600, 400));

        var rootFrame = new Frame();
        rootFrame.Navigate(typeof(VerifyGamePage), new SwitchPageParams
        {
            GameDir = gameDir,
            ParentWindow = newWindow
        });

        newWindow.Content = rootFrame;
        newWindow.Activate();
    }

    private void OpenMap_Click(object sender, RoutedEventArgs e)
    {
        var newWindow = new Window();
        newWindow.Title = "Title_TeyvatMap".GetLocalized();
        var hWnd = WindowNative.GetWindowHandle(newWindow);
        var winId = Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = AppWindow.GetFromWindowId(winId);
        appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 800));

        var rootFrame = new Frame();
        rootFrame.Navigate(typeof(MapPage), newWindow);

        newWindow.Content = rootFrame;
        newWindow.Activate();
    }

    private async void OpenAnnouncement_Click(object sender, RoutedEventArgs e)
    {
        await GameAnnouncementLauncher.OpenAsync();
    }

    #endregion
}
