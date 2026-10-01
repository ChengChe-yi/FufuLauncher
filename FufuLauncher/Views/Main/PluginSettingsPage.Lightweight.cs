/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage
{
    private void SyncModeTabs()
    {
        StandardModeTabButton.IsChecked = !ViewModel.IsLightweightMode;
        LightweightModeTabButton.IsChecked = ViewModel.IsLightweightMode;
    }

    private async void OnLightweightModeTabClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsLightweightMode)
        {
            SyncModeTabs();
            return;
        }

        if (!await ConfirmModeSwitchAsync("LightweightMode_EnterTitle", "LightweightMode_EnterContent", "LightweightMode_EnterConfirm"))
        {
            SyncModeTabs();
            return;
        }

        if (!await InstallLightweightPluginWithProgressAsync("LightweightMode_InstallTitle"))
        {
            SyncModeTabs();
            return;
        }

        var service = App.GetService<LightweightPluginService>();
        LightweightPluginService.DisableMainPluginIfPresent();
        await service.SetLightweightModeAsync(true);

        ApplyModeChange();
        WeakReferenceMessenger.Default.Send(new NotificationMessage(
            "Success".GetLocalized(),
            "LightweightMode_EnterSuccess".GetLocalized(),
            NotificationType.Success));
    }

    private async void OnStandardModeTabClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsLightweightMode)
        {
            SyncModeTabs();
            return;
        }

        if (App.GetService<ConstraintService>().IsRestricted)
        {
            SyncModeTabs();
            await ShowConstraintBlockedDialogAsync();
            return;
        }

        if (!await ConfirmModeSwitchAsync("LightweightMode_ExitTitle", "LightweightMode_ExitContent", "LightweightMode_ExitConfirm"))
        {
            SyncModeTabs();
            return;
        }

        var service = App.GetService<LightweightPluginService>();
        await service.SetLightweightModeAsync(false);

        LightweightPluginService.RemoveOrDisableLitePlugin();
        bool mainPluginRestored = LightweightPluginService.TryEnableMainPlugin();

        ApplyModeChange();

        WeakReferenceMessenger.Default.Send(new NotificationMessage(
            "Success".GetLocalized(),
            mainPluginRestored
                ? "LightweightMode_ExitSuccess".GetLocalized()
                : "LightweightMode_ExitSuccessNoMain".GetLocalized(),
            mainPluginRestored ? NotificationType.Success : NotificationType.Warning));
    }

    private async void OnReinstallLightweightPluginClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsLightweightMode)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "AdminWarningTitle".GetLocalized(),
                "LightweightMode_BlockLiteInstall".GetLocalized(),
                NotificationType.Warning,
                5000));
            return;
        }

        if (!await InstallLightweightPluginWithProgressAsync("LightweightMode_ReinstallTitle"))
        {
            return;
        }

        ViewModel.LoadConfiguration();
        ViewModel.RefreshPluginStates();

        WeakReferenceMessenger.Default.Send(new NotificationMessage(
            "Success".GetLocalized(),
            "LightweightMode_ReinstallSuccess".GetLocalized(),
            NotificationType.Success));
    }

    private void ApplyModeChange()
    {
        ViewModel.RefreshModeState();
        RestartMainPluginWatcher();
        SyncModeTabs();

        _hasShownMainPluginMissingWarning = false;
        ShowMainPluginMissingWarningIfNeeded();
    }

    private async Task<bool> ConfirmModeSwitchAsync(string titleKey, string contentKey, string primaryKey)
    {
        var dialog = new ContentDialog
        {
            Title = titleKey.GetLocalized(),
            Content = contentKey.GetLocalized(),
            PrimaryButtonText = primaryKey.GetLocalized(),
            CloseButtonText = "CancelBtn".GetLocalized(),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async Task<bool> InstallLightweightPluginWithProgressAsync(string titleKey)
    {
        var progressBar = new ProgressBar
        {
            Minimum = 0,
            Maximum = 100,
            Value = 0,
            Height = 20,
            Margin = new Thickness(0, 10, 0, 0)
        };
        var statusText = new TextBlock
        {
            Text = "LightweightMode_StatusResolving".GetLocalized(),
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var content = new StackPanel();
        content.Children.Add(statusText);
        content.Children.Add(progressBar);

        var progressDialog = new ContentDialog
        {
            Title = titleKey.GetLocalized(),
            Content = content,
            XamlRoot = XamlRoot
        };

        _ = progressDialog.ShowAsync();

        try
        {
            var progress = new Progress<double>(value => progressBar.Value = value);
            await App.GetService<LightweightPluginService>()
                .InstallOrUpdateLitePluginAsync(progress, text => statusText.Text = text);

            progressDialog.Hide();
            return true;
        }
        catch (Exception ex)
        {
            progressDialog.Hide();

            var failDialog = new ContentDialog
            {
                Title = "ErrorTitle".GetLocalized(),
                Content = string.Format("LightweightMode_InstallFailed".GetLocalized(), ex.Message),
                CloseButtonText = "CloseBtn".GetLocalized(),
                XamlRoot = XamlRoot
            };
            await failDialog.ShowAsync();
            return false;
        }
    }
}
