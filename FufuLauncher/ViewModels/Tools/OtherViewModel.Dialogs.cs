/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using System.Security.Principal;
using FufuLauncher.Activation;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.ViewModels;

public partial class OtherViewModel
{
    #region Dialogs & Permissions

    private bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    private async Task ShowAdminRequiredDialogAsync()
    {
        try
        {
            await _dispatcherQueue.EnqueueAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "AutoClicker_AdminRequiredTitle".GetLocalized(),
                    Content = "AutoClicker_AdminRequiredMessage".GetLocalized(),
                    CloseButtonText = "Common_GotIt".GetLocalized(),
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await dialog.ShowAsync();
            });
        }
        catch (Exception ex)
        {
            DebugLog.Info("OtherViewModel.Dialogs", $"Failed to show admin prompt dialog: {ex.Message}");
            StatusMessage = "AutoClicker_MissingAdminError".GetLocalized();
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        try
        {
            await _dispatcherQueue.EnqueueAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "Common_OperationFailed".GetLocalized(),
                    Content = message,
                    CloseButtonText = "Common_Confirm".GetLocalized(),
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await dialog.ShowAsync();
            });
        }
        catch (Exception ex)
        {
            DebugLog.Info("OtherViewModel.Dialogs", $"Failed to show error dialog: {ex.Message}");
            StatusMessage = string.Format("Common_Error_Format".GetLocalized(), message);
        }
    }

    private async Task<bool> ShowLatencyWarningDialogAsync()
    {
        bool result = false;
        try
        {
            await _dispatcherQueue.EnqueueAsync(async () =>
            {
                var dialog = new ContentDialog
                {
                    Title = "Common_RiskWarning".GetLocalized(),
                    Content = "AutoClicker_SecurityWarningMessage".GetLocalized(),
                    PrimaryButtonText = "AutoClicker_ConfirmEnable".GetLocalized(),
                    CloseButtonText = "Common_Cancel".GetLocalized(),
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                var dialogResult = await dialog.ShowAsync();
                result = dialogResult == ContentDialogResult.Primary;
            });
        }
        catch (Exception ex)
        {
            DebugLog.Info("OtherViewModel.Dialogs", $"Failed to show delay warning dialog: {ex.Message}");
        }

        return result;
    }

    #endregion
}