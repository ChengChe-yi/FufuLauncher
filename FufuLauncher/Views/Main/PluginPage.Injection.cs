/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class PluginPage
{
    #region 注入开关与诊断入口

    private async void OnInjectionToggled(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleSwitch toggleSwitch)
        {
            if (toggleSwitch.IsOn == MainViewModel.UseInjection) return;

            if (toggleSwitch.IsOn)
            {
                var osArch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
                if (osArch == System.Runtime.InteropServices.Architecture.Arm || 
                    osArch == System.Runtime.InteropServices.Architecture.Arm64)
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Plugin_ArchWarning_Title".GetLocalized(),
                        Content = "Plugin_ArchWarning_Content".GetLocalized(),
                        PrimaryButtonText = "Plugin_ArchWarning_Continue".GetLocalized(),
                        CloseButtonText = "CancelBtn".GetLocalized(),
                        XamlRoot = XamlRoot
                    };

                    var result = await dialog.ShowAsync();
                
                    if (result != ContentDialogResult.Primary)
                    {
                        toggleSwitch.IsOn = false;
                        return; 
                    }
                }
            }

            MainViewModel.UseInjection = toggleSwitch.IsOn;
        }
    }
    
    private void OnOpenDiagnosticsClick(object sender, RoutedEventArgs e)
    {
        var diagnosticsWindow = new DiagnosticsWindow();
        diagnosticsWindow.Activate();
    }

    #endregion
}
