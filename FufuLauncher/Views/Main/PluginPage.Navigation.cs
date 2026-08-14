/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

public sealed partial class PluginPage
{
    #region 插件配置导航

    private async void OnConfigClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is PluginItem item && item.HasConfig)
        {
            var folderName = new DirectoryInfo(item.DirectoryPath).Name;
            bool isFuFuPlugin = folderName.Contains("FuFuPlugin", StringComparison.OrdinalIgnoreCase);
            bool isFpsPlugin = folderName.Contains("FPS", StringComparison.OrdinalIgnoreCase);
            
            if (isFuFuPlugin || isFpsPlugin)
            {
                ExitStoryboard.Begin();
                await Task.Delay(300);
                Frame.Navigate(typeof(PluginSettingsPage), item, new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
            
                var navView = FindParentNavigationView(this);
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
            else
            {
                ExitStoryboard.Begin();
                await Task.Delay(300);
                Frame.Navigate(typeof(PluginConfigPage), item, new Microsoft.UI.Xaml.Media.Animation.SuppressNavigationTransitionInfo());
            }
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
