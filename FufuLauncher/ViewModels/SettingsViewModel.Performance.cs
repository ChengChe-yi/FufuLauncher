/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Services;
using FufuLauncher.Services.Background;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 性能与进程

    partial void OnIsCpuUsageWarningEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync(ProcessCpuUsageMonitor.IsEnabledSettingKey, value);
    }

    partial void OnCpuUsageWarningThresholdChanged(double value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync(ProcessCpuUsageMonitor.ThresholdSettingKey, Math.Clamp(value, 5.0, 100.0));
    }

    private async Task ResetCpuUsageWarningSettingsAsync()
    {
        IsCpuUsageWarningEnabled = true;
        CpuUsageWarningThreshold = ProcessCpuUsageMonitor.DefaultCpuThreshold;
        await _localSettingsService.SaveSettingAsync(ProcessCpuUsageMonitor.IsEnabledSettingKey, true);
        await _localSettingsService.SaveSettingAsync(ProcessCpuUsageMonitor.ThresholdSettingKey, ProcessCpuUsageMonitor.DefaultCpuThreshold);
    }

    partial void OnAppProcessPriorityChanged(AppProcessPriority value)
    {
        _localSettingsService.SaveSettingAsync("AppProcessPriority", (int)value);
        ApplyProcessPriority(value);
    }

    private void ApplyProcessPriority(AppProcessPriority priority)
    {
        try
        {
            var process = Process.GetCurrentProcess();
            switch (priority)
            {
                case AppProcessPriority.Normal:
                    process.PriorityClass = ProcessPriorityClass.Normal;
                    break;
                case AppProcessPriority.AboveNormal:
                    process.PriorityClass = ProcessPriorityClass.AboveNormal;
                    break;
                case AppProcessPriority.High:
                    process.PriorityClass = ProcessPriorityClass.High;
                    break;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"设置进程优先级失败: {ex.Message}");
        }
    }

    partial void OnIsBetterGIIntegrationEnabledChanged(bool value)
    {
        Debug.WriteLine($"SettingsViewModel: BetterGI联动设置变更为 {value}");
        _ = _localSettingsService.SaveSettingAsync("IsBetterGIIntegrationEnabled", value);
    }

    partial void OnIsBetterGICloseOnExitEnabledChanged(bool value)
    {
        Debug.WriteLine($"SettingsViewModel: BetterGI 关闭随游戏退出设置变更为 {value}");
        _ = _localSettingsService.SaveSettingAsync("IsBetterGICloseOnExitEnabled", value);
    }

    #endregion
}
