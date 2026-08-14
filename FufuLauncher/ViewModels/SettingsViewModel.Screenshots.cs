/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 截图

    partial void OnIsScreenshotEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsScreenshotEnabled", value);
    }

    partial void OnScreenshotHotkeyChanged(string value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("ScreenshotHotkey", value);
    }

    partial void OnScreenshotSavePathChanged(string value)
    {
        if (_isInitializing) return;
        HasScreenshotSavePath = !string.IsNullOrEmpty(value);
        _ = _localSettingsService.SaveSettingAsync("ScreenshotSavePath", value);
    }

    private async Task SelectScreenshotFolderAsync()
    {
        try
        {
            var folder = await _filePickerService.PickFolderAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                ScreenshotSavePath = folder;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"选择截图文件夹失败: {ex.Message}");
        }
    }

    private async Task ClearScreenshotFolderAsync()
    {
        ScreenshotSavePath = null;
        HasScreenshotSavePath = false;
        await _localSettingsService.SaveSettingAsync<string>("ScreenshotSavePath", null);
    }

    private async Task OpenScreenshotFolderAsync()
    {
        var path = ScreenshotSavePath;
        if (string.IsNullOrEmpty(path))
        {
            path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "FufuScreenshots");
        }

        if (Directory.Exists(path))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        else
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        await Task.CompletedTask;
    }

    #endregion
}
