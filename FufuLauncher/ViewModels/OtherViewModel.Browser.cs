/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Views;

namespace FufuLauncher.ViewModels;

public partial class OtherViewModel
{
    #region Browser Window

    private void OpenBrowserWindow()
    {
        try
        {
            if (_dispatcherQueue.HasThreadAccess)
            {
                var newWindow = new BrowserWindow();
                newWindow.Activate();
            }
            else
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    var newWindow = new BrowserWindow();
                    newWindow.Activate();
                });
            }
            Debug.WriteLine("[OtherViewModel] 浏览器窗口已创建");
        }
        catch (Exception ex)
        {
            StatusMessage = $"打开浏览器失败: {ex.Message}";
            Debug.WriteLine($"[OtherViewModel] 打开浏览器失败: {ex.Message}");
        }
    }

    #endregion
}
