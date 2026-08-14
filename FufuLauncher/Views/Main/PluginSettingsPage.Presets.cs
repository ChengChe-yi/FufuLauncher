/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using FufuLauncher.ViewModels;
using FufuLauncher.Helpers;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage
{
    private async void OnSwitchPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is PresetModel preset)
        {
            if (preset.IsLocked)
            {
                var reason = ViewModel.GetPresetLockReason(preset);
                var dialog = new ContentDialog
                {
                    Title = "Preset_Locked_Title".GetLocalized(),
                    Content = string.Format("Preset_Locked_Content_Format".GetLocalized(), reason),
                    PrimaryButtonText = "Preset_Locked_Continue".GetLocalized(),
                    CloseButtonText = "CancelBtn".GetLocalized(),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                ViewModel.ForceUnlockAndSwitchPreset(preset);
                return;
            }
            ViewModel.SwitchPreset(preset);
        }
    }

    private async void OnCreateNewPresetClick(object sender, RoutedEventArgs e)
    {
        var inputTextBox = new TextBox { PlaceholderText = "Preset_Create_Placeholder".GetLocalized() };
        var dialog = new ContentDialog
        {
            Title = "Preset_Create_Title".GetLocalized(),
            Content = inputTextBox,
            PrimaryButtonText = "LanguageSelection_Confirm".GetLocalized(),
            CloseButtonText = "CancelBtn".GetLocalized(),
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputTextBox.Text))
        {
            var currentData = ViewModel.CurrentPreset?.ConfigData;
            var currentHash = ViewModel.CurrentPreset?.DllHash ?? "";
            
            if (currentData != null)
            {
                var newPreset = ViewModel.CreateNewPreset(inputTextBox.Text.Trim(), currentData, currentHash);
                ViewModel.SwitchPreset(newPreset);
            }
        }
    }

    private async void OnDeletePresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is PresetModel preset)
        {
            var dialog = new ContentDialog
            {
                Title = "AdminWarningTitle".GetLocalized(),
                Content = string.Format("Preset_Delete_Confirm_Format".GetLocalized(), preset.Name),
                PrimaryButtonText = "Preset_Delete_ConfirmBtn".GetLocalized(),
                CloseButtonText = "CancelBtn".GetLocalized(),
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                ViewModel.DeletePreset(preset);
            }
        }
    }

    private async void OnResetAllPresetsClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Preset_ResetAll_Title".GetLocalized(),
            Content = "Preset_ResetAll_Content".GetLocalized(),
            PrimaryButtonText = "Preset_ResetAll_ConfirmBtn".GetLocalized(),
            CloseButtonText = "CancelBtn".GetLocalized(),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };

        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            ViewModel.ClearAllPresets();

            if (ViewModel.SelectedPluginIndex == 0)
            {
                string urlLatest = "http://kr2-proxy.gitwarp.top:9980/https://github.com/CodeCubist/FufuLauncher--Plugins/blob/main/FuFuPlugin.zip";
                await DownloadAndInstallPluginAsync(urlLatest);
            }
            else if (ViewModel.SelectedPluginIndex == 1)
            {
                await PerformFpsPluginRepairAsync(true);
            }
        }
    }
}
