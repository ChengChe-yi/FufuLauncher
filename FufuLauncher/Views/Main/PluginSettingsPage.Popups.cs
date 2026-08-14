/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FufuLauncher.Helpers;
using Windows.System;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage
{
    private FeedbackWindow _feedbackWindow;
    private Window _prWindow;

    private void OnOpenSponsorWindowClick(object sender, RoutedEventArgs e)
    {
        var sponsorWindow = new SponsorWindow();

        IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(sponsorWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

        if (appWindow != null)
        {
            string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets/WindowIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            var size = new Windows.Graphics.SizeInt32(640, 520);
            appWindow.Resize(size);

            var displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                var centeredX = (displayArea.WorkArea.Width - size.Width) / 2;
                var centeredY = (displayArea.WorkArea.Height - size.Height) / 2;
                appWindow.Move(new Windows.Graphics.PointInt32(centeredX, centeredY));
            }

            var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
            if (presenter != null)
            {
                presenter.IsMaximizable = false;
                presenter.IsResizable = false;
            }
        }

        sponsorWindow.Activate();
    }

    private void OnFeedbackClick(object sender, RoutedEventArgs e)
    {
        if (_feedbackWindow == null)
        {
            _feedbackWindow = new FeedbackWindow();
            _feedbackWindow.Closed += (s, args) => _feedbackWindow = null;
        }
        _feedbackWindow.Activate();
    }

    private void OnPullRequestsClick(object sender, RoutedEventArgs e)
    {
        if (_prWindow == null)
        {
            _prWindow = new Window();
            _prWindow.Title = "PR_Window_Title".GetLocalized();
            _prWindow.Closed += (s, args) => _prWindow = null;

            _prWindow.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            _prWindow.ExtendsContentIntoTitleBar = true;

            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(_prWindow);
            Microsoft.UI.WindowId windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            Microsoft.UI.Windowing.AppWindow appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32(600, 450));
            
            var titleBarGrid = new Grid { Height = 32 };
            var titleText = new TextBlock
            {
                Text = "PR_Window_Title".GetLocalized(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0),
                FontSize = 12
            };
            titleBarGrid.Children.Add(titleText);
            
            var contentStackPanel = new StackPanel 
            { 
                Padding = new Thickness(24, 16, 24, 24),
                Spacing = 16 
            };

            var textBlock = new TextBlock
            {
                Text = "PR_Description".GetLocalized(),
                TextWrapping = TextWrapping.Wrap
            };

            var openLinkBtn = new Button
            {
                Content = "PR_GitHubButton".GetLocalized(),
                HorizontalAlignment = HorizontalAlignment.Left
            };

            openLinkBtn.Click += async (s, args) => 
            { 
                await Launcher.LaunchUriAsync(new Uri("https://github.com/FufuLauncher/FufuLauncher/pulls")); 
            };

            contentStackPanel.Children.Add(textBlock);
            contentStackPanel.Children.Add(openLinkBtn);
            
            var rootGrid = new Grid();
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid.SetRow(titleBarGrid, 0);
            Grid.SetRow(contentStackPanel, 1);

            rootGrid.Children.Add(titleBarGrid);
            rootGrid.Children.Add(contentStackPanel);

            _prWindow.Content = rootGrid;
            
            _prWindow.SetTitleBar(titleBarGrid);
        }
        _prWindow.Activate();
    }
}
