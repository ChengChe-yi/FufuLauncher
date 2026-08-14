/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Controls;
using WinRT.Interop;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    #region 信息与提示对话框

    private async void OnIndependentDeploymentClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Settings_Disclaimer".GetLocalized(),
            Content = "该独立部署版本软件由PR贡献者自行开发提供，FufuLauncher无法对该软件的安全性、稳定性或后续维护提供任何保证，您需要自行辨别使用风险\n\n是否继续访问该项目地址？",
            PrimaryButtonText = "Settings_ContinueVisit".GetLocalized(),
            CloseButtonText = "CancelBtn".GetLocalized(),
            XamlRoot = this.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            _ = Windows.System.Launcher.LaunchUriAsync(new Uri("https://github.com/Marchen-orz/MiyoQian"));
        }
    }

    private void OnIdentifyMonitorsClick(object sender, RoutedEventArgs e)
    {
        var displayAreas = DisplayArea.FindAll();
        for (int i = 0; i < displayAreas.Count; i++)
        {
            int index = i + 1;
            var displayArea = displayAreas[i];

            var window = new Window();
            window.ExtendsContentIntoTitleBar = true;

            var grid = new Grid
            {
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.8 },
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var textBlock = new TextBlock
            {
                Text = index.ToString(),
                FontSize = 140,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.White),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold
            };

            grid.Children.Add(textBlock);
            window.Content = grid;

            IntPtr hWnd = WindowNative.GetWindowHandle(window);
            WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow != null)
            {
                var presenter = appWindow.Presenter as OverlappedPresenter;
                if (presenter != null)
                {
                    presenter.SetBorderAndTitleBar(false, false);
                    presenter.IsAlwaysOnTop = true;
                }

                var size = new Windows.Graphics.SizeInt32(250, 250);
                appWindow.Resize(size);

                var centeredX = displayArea.WorkArea.X + (displayArea.WorkArea.Width - size.Width) / 2;
                var centeredY = displayArea.WorkArea.Y + (displayArea.WorkArea.Height - size.Height) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(centeredX, centeredY));
            }

            window.Activate();

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, args) =>
            {
                window.Close();
                ((DispatcherTimer)s).Stop();
            };
            timer.Start();
        }
    }

    private async void OnCommunityCheckinInfoClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Settings_CommunityCheckinNote".GetLocalized(),
            Content = "由于米游社逐步删除了互动获取米游币渠道，下方选项大概并不能让获取米游币变得更多，等待后续官方更新新策略",
            CloseButtonText = "GotItBtn".GetLocalized(),
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void OnCloudGameCheckinInfoClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Settings_CloudCheckinNote".GetLocalized(),
            Content = "开启云游戏签到需要对应账号添加云游戏登录凭证，否则跳过云游戏签到",
            CloseButtonText = "GotItBtn".GetLocalized(),
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void OnCpuUsageWarningToggled(object sender, RoutedEventArgs e)
    {
        if (!_cpuUsageWarningToggleLoaded)
        {
            _cpuUsageWarningToggleLoaded = true;
            return;
        }

        if (sender is ToggleSwitch { IsOn: false })
        {
            var dialog = new ContentDialog
            {
                Title = "已关闭 CPU 占用异常警告",
                Content = "关闭后，启动器即使长期高 CPU 占用也不会再主动提示。若遇到卡顿、发热或异常耗电，请自行留意并及时反馈问题。",
                CloseButtonText = "GotItBtn".GetLocalized(),
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    #endregion
}
