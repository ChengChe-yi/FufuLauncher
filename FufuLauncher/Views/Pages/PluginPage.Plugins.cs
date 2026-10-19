/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class PluginPage
{
    #region 插件列表操作

    private async void OnPluginToggled(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleSwitch toggleSwitch || toggleSwitch.Tag is not PluginItem item) return;

        if (toggleSwitch.IsOn != item.IsEnabled)
        {
            if (toggleSwitch.IsOn &&
                LightweightPluginService.IsMainPluginPackage(Path.GetFileName(item.DirectoryPath), item.FileName) &&
                App.GetService<ConstraintService>().IsRestricted)
            {
                toggleSwitch.IsOn = item.IsEnabled;
                await ShowConstraintBlockedDialogAsync();
                return;
            }

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

    private async Task ShowConstraintBlockedDialogAsync()
    {
        var message = await App.GetService<ConstraintService>().GetBlockMessageAsync();

        var dialog = new ContentDialog
        {
            Title = "Constraint_BlockedTitle".GetLocalized(),
            Content = message,
            CloseButtonText = "GotItBtn".GetLocalized(),
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is PluginItem pluginItem)
        {
            var currentFolderName = new DirectoryInfo(pluginItem.DirectoryPath).Name;

            var dialog = new ContentDialog
            {
                Title = "PluginPage_RenameFolderTitle".GetLocalized(),
                PrimaryButtonText = "OkBtn".GetLocalized(),
                CloseButtonText = "CancelBtn".GetLocalized(),
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