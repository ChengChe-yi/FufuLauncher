/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Security.Principal;
using FufuLauncher.Activation;
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
                    Title = "需要管理员权限",
                    Content = "使用全局连点器功能需要管理员权限才能正常拦截和发送按键\n\n请关闭本程序，右键选择“以管理员身份运行”后再次尝试",
                    CloseButtonText = "我知道了",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await dialog.ShowAsync();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"显示管理员提示对话框失败: {ex.Message}");
            StatusMessage = "错误: 缺少管理员权限，请重启程序";
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
                    Title = "操作失败",
                    Content = message,
                    CloseButtonText = "确定",
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                await dialog.ShowAsync();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"显示错误对话框失败: {ex.Message}");
            StatusMessage = $"错误: {message}";
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
                    Title = "风险提示",
                    Content = "开启连点器功能将会安装全局键盘和鼠标拦截钩子，这可能会导致输入操作出现轻微延迟\n\n您确定要开启此功能吗？",
                    PrimaryButtonText = "确认开启",
                    CloseButtonText = "取消",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = App.MainWindow.Content.XamlRoot
                };
                var dialogResult = await dialog.ShowAsync();
                result = dialogResult == ContentDialogResult.Primary;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"显示延迟警告对话框失败: {ex.Message}");
        }
        return result;
    }

    #endregion
}
