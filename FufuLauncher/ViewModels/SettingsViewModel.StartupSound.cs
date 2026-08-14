/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;

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
            Debug.WriteLine($"选择启动语音失败: {ex.Message}");
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
            Debug.WriteLine($"清除启动语音失败: {ex.Message}");
        }
    }

    partial void OnIsStartupSoundEnabledChanged(bool value)
    {
        Debug.WriteLine($"SettingsViewModel: 保存启动语音开关 {value}");
        _ = _localSettingsService.SaveSettingAsync("IsStartupSoundEnabled", value);
    }

    #endregion
}
