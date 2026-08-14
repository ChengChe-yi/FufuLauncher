/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
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
                        Title = "架构兼容性警告",
                        Content = "您的电脑可能为ARM架构，注入功能在ARM架构的电脑中不可用，是否确认继续开启？",
                        PrimaryButtonText = "继续开启",
                        CloseButtonText = "取消",
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
