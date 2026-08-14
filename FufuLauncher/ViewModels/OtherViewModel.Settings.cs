/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Services;
using Windows.System;

namespace FufuLauncher.ViewModels;

public partial class OtherViewModel
{
    #region Settings Persistence

    private void LoadSettings()
    {
        try
        {
            Debug.WriteLine("[OtherViewModel] 开始加载配置...");

            var enabled = _localSettingsService.ReadSettingAsync("AdditionalProgramEnabled").Result;
            var path = _localSettingsService.ReadSettingAsync("AdditionalProgramPath").Result;
            IsAdditionalProgramEnabled = enabled != null && Convert.ToBoolean(enabled);
            AdditionalProgramPath = path?.ToString()?.Trim('"') ?? string.Empty;

            var autoClickerEnabled = _localSettingsService.ReadSettingAsync("AutoClickerEnabled").Result;
            var triggerKey = _localSettingsService.ReadSettingAsync("AutoClickerTriggerKey").Result;
            var clickKey = _localSettingsService.ReadSettingAsync("AutoClickerClickKey").Result;
            var stopKey = _localSettingsService.ReadSettingAsync("AutoClickerStopKey").Result;
            var mode = _localSettingsService.ReadSettingAsync("AutoClickerMode").Result;

            Debug.WriteLine($"[OtherViewModel] 原始配置 - Enabled: {autoClickerEnabled}, TriggerKey: {triggerKey}, ClickKey: {clickKey}, StopKey: {stopKey}, Mode: {mode}");

            _isInitializing = true;
            TriggerKey = triggerKey?.ToString()?.Trim('"') ?? "F";
            ClickKey = clickKey?.ToString()?.Trim('"') ?? "F";
            StopKey = stopKey?.ToString()?.Trim('"') ?? string.Empty;

            var modeStr = mode?.ToString()?.Trim('"') ?? AutoClickerMode.Keyboard.ToString();
            if (!Enum.TryParse<AutoClickerMode>(modeStr, out var clickerMode))
            {
                clickerMode = AutoClickerMode.Keyboard;
            }

            IsMouseLeftClickerEnabled = clickerMode == AutoClickerMode.MouseLeft;
            IsMouseRightClickerEnabled = clickerMode == AutoClickerMode.MouseRight;
            IsAutoClickerEnabled = autoClickerEnabled != null && Convert.ToBoolean(autoClickerEnabled);

            if (Enum.TryParse<VirtualKey>(TriggerKey, out var tk))
            {
                _autoClickerService.TriggerKey = tk;
                Debug.WriteLine($"[OtherViewModel] 触发键解析成功: {tk}");
            }

            if (Enum.TryParse<VirtualKey>(ClickKey, out var ck))
            {
                _autoClickerService.ClickKey = ck;
                Debug.WriteLine($"[OtherViewModel] 连点键解析成功: {ck}");
            }

            if (!string.IsNullOrWhiteSpace(StopKey) && Enum.TryParse<VirtualKey>(StopKey, out var sk))
            {
                _autoClickerService.StopKey = sk;
            }
            else
            {
                _autoClickerService.StopKey = VirtualKey.None;
            }

            _autoClickerService.Mode = clickerMode;
            _isInitializing = false;

            if (IsMouseModeEnabled() && !HasStopKey())
            {
                IsAutoClickerEnabled = false;
                IsMouseLeftClickerEnabled = false;
                IsMouseRightClickerEnabled = false;
                _autoClickerService.Mode = AutoClickerMode.Keyboard;
                StatusMessage = "鼠标连点必须先设置键盘停止快捷键";
                _ = SaveSettingsAsync();
            }
            else
            {
                _autoClickerService.IsEnabled = IsAutoClickerEnabled;
            }

            Debug.WriteLine($"[OtherViewModel] 最终配置 - 启用: {IsAutoClickerEnabled}, 模式: {GetCurrentMode()}, 触发键: {TriggerKey}, 连点键: {ClickKey}, 停止键: {StopKey}");
        }
        catch (Exception ex)
        {
            _isInitializing = false;
            Debug.WriteLine($"[OtherViewModel] 加载配置失败: {ex.Message}");
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            string cleanPath = AdditionalProgramPath.Trim('"');
            await _localSettingsService.SaveSettingAsync("AdditionalProgramEnabled", IsAdditionalProgramEnabled);
            await _localSettingsService.SaveSettingAsync("AdditionalProgramPath", cleanPath);
            await _localSettingsService.SaveSettingAsync("AutoClickerEnabled", IsAutoClickerEnabled);

            await _localSettingsService.SaveSettingAsync("AutoClickerTriggerKey", TriggerKey);
            await _localSettingsService.SaveSettingAsync("AutoClickerClickKey", ClickKey);
            await _localSettingsService.SaveSettingAsync("AutoClickerStopKey", StopKey);
            await _localSettingsService.SaveSettingAsync("AutoClickerMode", GetCurrentMode().ToString());

            Debug.WriteLine($"[连点器] 配置保存成功 - 启用: {IsAutoClickerEnabled}, 模式: {GetCurrentMode()}, 触发键: {TriggerKey}, 连点键: {ClickKey}, 停止键: {StopKey}");

            _ = Task.Delay(2000).ContinueWith(_ =>
                _dispatcherQueue?.TryEnqueue(() => StatusMessage = string.Empty));
            AdditionalProgramPath = cleanPath;
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败: {ex.Message}";
            Debug.WriteLine($"[连点器] 配置保存失败: {ex.Message}");
        }
    }

    #endregion
}
