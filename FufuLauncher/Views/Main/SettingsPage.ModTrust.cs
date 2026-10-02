/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Services.CodeSigning;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    private CodeSigningTrustService TrustService => App.GetService<CodeSigningTrustService>();

    private async Task RefreshModTrustUiAsync()
    {
        try
        {
            var status = await Task.Run(() => TrustService.GetStatus());

            if (ModTrustStatusText != null)
            {
                ModTrustStatusText.Text = BuildStatusText(status);
            }

            if (ModTrustRootInfoText != null)
            {
                var info = status.PackageVerified
                    ? string.Empty
                    : status.PackageError ?? "ModTrust_PackageMissing".GetLocalized();

                ModTrustRootInfoText.Text = info;
                ModTrustRootInfoText.Visibility = string.IsNullOrEmpty(info) ? Visibility.Collapsed : Visibility.Visible;
            }

            if (ModTrustStrictToggle != null)
            {
                var mode = ModTrustGate.ReadMode();
                ModTrustStrictToggle.IsOn = mode == ModTrustEnforcement.Enforce;
                ModTrustStrictToggle.IsEnabled = status.PackageVerified;
            }

            if (ModTrustInstallUserButton != null)
            {
                ModTrustInstallUserButton.IsEnabled = status.PackageVerified &&
                                                      !status.MachineProvidesTrust &&
                                                      !status.UserScopeManaged;
            }

            if (ModTrustUninstallUserButton != null)
            {
                ModTrustUninstallUserButton.IsEnabled = status.PackageVerified && status.UserScopeManaged;
            }

            if (ModTrustInstallMachineButton != null)
            {
                ModTrustInstallMachineButton.IsEnabled = status.PackageVerified && !status.MachineScopeManaged;
            }

            if (ModTrustUninstallMachineButton != null)
            {
                ModTrustUninstallMachineButton.IsEnabled = status.PackageVerified && status.MachineScopeManaged;
            }

            if (ModTrustSyncButton != null)
            {
                ModTrustSyncButton.IsEnabled = CodeSigningTrustService.GetPinnedRootThumbprint() != null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModTrust] 刷新信任状态失败: {ex}");
        }
    }

    private static string BuildStatusText(TrustServiceStatus status)
    {
        if (!status.PackageVerified)
        {
            return status.PackageError ?? "ModTrust_PackageMissing".GetLocalized();
        }

        string installed;

        if (status.UserScopeManaged && status.MachineProvidesTrust)
        {
            installed = "ModTrust_InstalledBoth".GetLocalized();
        }
        else if (status.MachineProvidesTrust)
        {
            installed = "ModTrust_InstalledMachineEffective".GetLocalized();
        }
        else if (status.UserScopeManaged)
        {
            installed = "ModTrust_InstalledUser".GetLocalized();
        }
        else
        {
            installed = "ModTrust_NotInstalled".GetLocalized();
        }

        var text = string.Format("ModTrust_StatusFormat".GetLocalized(), installed,
            status.SyncEndpoint ?? CodeSigningTrustService.DefaultSyncEndpoint);

        if (status.MachineProvidesTrust && !status.UserScopeManaged)
        {
            text += Environment.NewLine + "ModTrust_MachineHint".GetLocalized();
        }

        return text;
    }

    private async void OnModTrustRefreshClick(object sender, RoutedEventArgs e)
    {
        await RefreshModTrustUiAsync();
    }

    private async void OnModTrustInstallUserClick(object sender, RoutedEventArgs e)
    {
        await InstallOrUninstallAsync(TrustStoreScope.CurrentUser, install: true);
    }

    private async void OnModTrustUninstallUserClick(object sender, RoutedEventArgs e)
    {
        await InstallOrUninstallAsync(TrustStoreScope.CurrentUser, install: false);
    }

    private async void OnModTrustInstallMachineClick(object sender, RoutedEventArgs e)
    {
        await InstallOrUninstallAsync(TrustStoreScope.LocalMachine, install: true);
    }

    private async void OnModTrustUninstallMachineClick(object sender, RoutedEventArgs e)
    {
        await InstallOrUninstallAsync(TrustStoreScope.LocalMachine, install: false);
    }

    private async void OnModTrustStrictToggled(object sender, RoutedEventArgs e)
    {
        if (ModTrustStrictToggle == null) return;

        var mode = ModTrustStrictToggle.IsOn ? ModTrustEnforcement.Enforce : ModTrustEnforcement.Warn;

        if (mode == ModTrustEnforcement.Enforce)
        {
            var confirmed = await ShowSafeDialogAsync(new ContentDialog
            {
                Title = "ModTrust_StrictTitle".GetLocalized(),
                Content = "ModTrust_StrictConfirm".GetLocalized(),
                PrimaryButtonText = "OkBtn".GetLocalized(),
                CloseButtonText = "CancelBtn".GetLocalized(),
                XamlRoot = XamlRoot
            });

            if (confirmed != ContentDialogResult.Primary)
            {
                ModTrustStrictToggle.IsOn = false;
                return;
            }
        }

        ModTrustGate.WriteMode(mode);
        Debug.WriteLine($"[ModTrust] 信任策略模式已切换为 {mode}");
    }

    private async void OnModTrustSyncClick(object sender, RoutedEventArgs e)
    {
        if (ModTrustSyncButton != null) ModTrustSyncButton.IsEnabled = false;

        try
        {
            var confirmed = await ShowSafeDialogAsync(new ContentDialog
            {
                Title = "ModTrust_SyncTitle".GetLocalized(),
                Content = "ModTrust_SyncConfirm".GetLocalized(),
                PrimaryButtonText = "OkBtn".GetLocalized(),
                CloseButtonText = "CancelBtn".GetLocalized(),
                XamlRoot = XamlRoot
            });

            if (confirmed != ContentDialogResult.Primary) return;

            var result = await TrustService.SyncFromEndpointAsync();

            await ShowSafeDialogAsync(new ContentDialog
            {
                Title = result.Ok ? "Success".GetLocalized() : "Failure".GetLocalized(),
                Content = result.Message,
                CloseButtonText = "OkBtn".GetLocalized(),
                XamlRoot = XamlRoot
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ModTrust] 同步信任包失败: {ex}");
            await ShowSafeDialogAsync(new ContentDialog
            {
                Title = "ErrorTitle".GetLocalized(),
                Content = ex.Message,
                CloseButtonText = "OkBtn".GetLocalized(),
                XamlRoot = XamlRoot
            });
        }
        finally
        {
            if (ModTrustSyncButton != null) ModTrustSyncButton.IsEnabled = true;
            await RefreshModTrustUiAsync();
        }
    }

    private async Task InstallOrUninstallAsync(TrustStoreScope scope, bool install)
    {
        var status = await Task.Run(() => TrustService.GetStatus());
        if (!status.PackageVerified)
        {
            await ShowSafeDialogAsync(new ContentDialog
            {
                Title = "ModTrust_Title".GetLocalized(),
                Content = status.PackageError ?? "ModTrust_PackageMissing".GetLocalized(),
                CloseButtonText = "OkBtn".GetLocalized(),
                XamlRoot = XamlRoot
            });
            return;
        }

        var result = install
            ? await TrustInstallFlow.InstallAsync(TrustService, XamlRoot, scope)
            : await TrustInstallFlow.UninstallAsync(TrustService, XamlRoot, scope);

        if (result == null) return;

        await ShowSafeDialogAsync(new ContentDialog
        {
            Title = result.Ok ? "Success".GetLocalized() : "Failure".GetLocalized(),
            Content = result.Message,
            CloseButtonText = "OkBtn".GetLocalized(),
            XamlRoot = XamlRoot
        });

        await RefreshModTrustUiAsync();
    }
}
