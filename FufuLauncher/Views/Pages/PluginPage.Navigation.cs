/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Models;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

public sealed partial class PluginPage
{
    #region 插件配置导航

    private bool _isConfigNavigating;

    private async void OnConfigClick(object sender, RoutedEventArgs e)
    {
        if (_isConfigNavigating || sender is not Button { Tag: PluginItem item } || !item.HasConfig)
        {
            return;
        }

        var frame = Frame;
        if (frame == null || !ReferenceEquals(frame.Content, this))
        {
            return;
        }

        _isConfigNavigating = true;
        try
        {
            var folderName = new DirectoryInfo(item.DirectoryPath).Name;
            var isOfficialPlugin = folderName.Contains("FuFuPlugin", StringComparison.OrdinalIgnoreCase) ||
                                   folderName.Contains("FPS", StringComparison.OrdinalIgnoreCase) ||
                                   folderName.Contains(LightweightPluginService.LitePluginFolderName,
                                       StringComparison.OrdinalIgnoreCase);
            var navView = isOfficialPlugin ? FindParentNavigationView(this) : null;
            var pageType = isOfficialPlugin ? typeof(PluginSettingsPage) : typeof(PluginConfigPage);

            ExitStoryboard.Begin();
            await Task.Delay(300);
            if (!ReferenceEquals(frame.Content, this))
            {
                return;
            }

            if (!frame.Navigate(pageType, item,
                    new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo()))
            {
                ExitStoryboard.Stop();
                EntranceStoryboard.Begin();
                return;
            }

            if (navView != null)
            {
                foreach (var menuItem in navView.MenuItems)
                {
                    if (menuItem is NavigationViewItem navItem &&
                        navItem.Tag?.ToString() == "FufuLauncher.ViewModels.PluginSettingsViewModel")
                    {
                        navView.SelectedItem = navItem;
                        break;
                    }
                }
            }
        }
        finally
        {
            _isConfigNavigating = false;
        }
    }

    private NavigationView FindParentNavigationView(DependencyObject child)
    {
        DependencyObject parentObject = VisualTreeHelper.GetParent(child);
        if (parentObject == null) return null;

        if (parentObject is NavigationView parent) return parent;

        return FindParentNavigationView(parentObject);
    }

    #endregion
}