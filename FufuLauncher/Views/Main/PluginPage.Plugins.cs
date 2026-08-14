/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class PluginPage
{
    #region 插件列表操作

    private void OnPluginToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch && toggleSwitch.Tag is PluginItem item)
        {
            if (toggleSwitch.IsOn != item.IsEnabled)
            {
                if (ViewModel.TogglePluginCommand.CanExecute(item))
                {
                    ViewModel.TogglePluginCommand.Execute(item);
                }
                else
                {
                    toggleSwitch.IsOn = item.IsEnabled;
                }
            }
        }
    }
    
    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is PluginItem pluginItem)
        {
            var currentFolderName = new DirectoryInfo(pluginItem.DirectoryPath).Name;

            var dialog = new ContentDialog
            {
                Title = "重命名插件文件夹",
                PrimaryButtonText = "确定",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = XamlRoot
            };

            var inputTextBox = new TextBox { Text = currentFolderName, AcceptsReturn = false };
            dialog.Content = inputTextBox;

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var newName = inputTextBox.Text.Trim();
                if (!string.IsNullOrEmpty(newName) && newName != currentFolderName)
                {
                    ViewModel.PerformRename(pluginItem, newName);
                }
            }
        }
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is PluginItem pluginItem)
        {
            if (ViewModel.DeletePluginCommand.CanExecute(pluginItem))
            {
                ViewModel.DeletePluginCommand.Execute(pluginItem);
            }
        }
    }

    #endregion
}
