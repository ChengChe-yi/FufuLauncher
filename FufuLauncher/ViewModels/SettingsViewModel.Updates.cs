/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Services.PluginMirror;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 更新与公告

    partial void OnIsUseThirdPartyCDNEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsUseThirdPartyCDNEnabled", value);
    }

    partial void OnIsPreviewUpdateAnnouncementEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsPreviewUpdateAnnouncementEnabled", value);
    }

    partial void OnIsPluginMirrorAccelerationEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync(PluginMirrorDownloadService.SettingKey, value);
    }

    private void CheckUpdate()
    {
        LaunchUpdater();
    }

    private void CheckPreviewUpdate()
    {
        LaunchUpdater(isPreview: true);
    }

    private void CheckRollback()
    {
        LaunchUpdater(rollback: true);
    }

    private void LaunchUpdater(bool isPreview = false, bool rollback = false)
    {
        try
        {
            string updaterPath = Path.Combine(AppContext.BaseDirectory, "UpdateFufuLauncher.exe");
            
            if (File.Exists(updaterPath))
            {
                var arguments = $"--use-third-party-cdn={IsUseThirdPartyCDNEnabled.ToString().ToLower()}" +
                                $" --installed-version={AppVersionHelper.FullVersion}";
                if (isPreview)
                {
                    arguments += " --preview";
                }
                if (rollback)
                {
                    arguments += " --rollback";
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = updaterPath,
                    UseShellExecute = true,
                    Verb = "runas",
                    Arguments = arguments
                };
                Process.Start(startInfo);
            }
            else
            {
                Debug.WriteLine("未找到 UpdateFufuLauncher.exe");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动更新程序失败: {ex.Message}");
        }
    }

    private static string GetVersionDescription()
    {
        return $"FufuLauncher - {AppVersionHelper.FullVersion}";
    }

    #endregion
}
