/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Helpers;
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

            DebugLog.Info("OtherViewModel.Browser", "[OtherViewModel] Browser window created");
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("Other_OpenBrowserFailed_Format".GetLocalized(), ex.Message);
            DebugLog.Info("OtherViewModel.Browser", $"[OtherViewModel] Failed to open browser: {ex.Message}");
        }
    }

    #endregion
}