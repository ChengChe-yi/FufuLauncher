/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Helpers;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 启动音效

    private async Task SelectStartupSoundAsync()
    {
        try
        {
            var path = await _filePickerService.PickAudioFileAsync();
            if (!string.IsNullOrEmpty(path))
            {
                StartupSoundPath = path;
                HasCustomStartupSound = true;

                await _localSettingsService.SaveSettingAsync<string>("StartupSoundPath", path);
            }
        }
        catch (Exception ex)
        {
            DebugLog.Info("SettingsViewModel.StartupSound", $"Failed to select startup sound: {ex.Message}");
        }
    }

    private async Task ClearStartupSound()
    {
        try
        {
            await _localSettingsService.SaveSettingAsync<string>("StartupSoundPath", null);
            StartupSoundPath = null;
            HasCustomStartupSound = false;
        }
        catch (Exception ex)
        {
            DebugLog.Info("SettingsViewModel.StartupSound", $"Failed to clear startup sound: {ex.Message}");
        }
    }

    partial void OnIsStartupSoundEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        DebugLog.Info("SettingsViewModel.StartupSound", $"SettingsViewModel: saved startup sound toggle {value}");
        _ = _localSettingsService.SaveSettingAsync("IsStartupSoundEnabled", value);
    }

    #endregion
}