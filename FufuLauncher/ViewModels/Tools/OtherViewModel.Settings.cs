/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Helpers;
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
            DebugLog.Info("OtherViewModel.Settings", "[OtherViewModel] Loading config...");

            var enabled = _localSettingsService.ReadSettingAsync("AdditionalProgramEnabled").Result;
            var path = _localSettingsService.ReadSettingAsync("AdditionalProgramPath").Result;
            IsAdditionalProgramEnabled = enabled != null && Convert.ToBoolean(enabled);
            AdditionalProgramPath = path?.ToString()?.Trim('"') ?? string.Empty;

            var autoClickerEnabled = _localSettingsService.ReadSettingAsync("AutoClickerEnabled").Result;
            var triggerKey = _localSettingsService.ReadSettingAsync("AutoClickerTriggerKey").Result;
            var clickKey = _localSettingsService.ReadSettingAsync("AutoClickerClickKey").Result;
            var stopKey = _localSettingsService.ReadSettingAsync("AutoClickerStopKey").Result;
            var mode = _localSettingsService.ReadSettingAsync("AutoClickerMode").Result;

            DebugLog.Info("OtherViewModel.Settings",
                $"[OtherViewModel] Raw config - Enabled: {autoClickerEnabled}, TriggerKey: {triggerKey}, ClickKey: {clickKey}, StopKey: {stopKey}, Mode: {mode}");

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
                DebugLog.Info("OtherViewModel.Settings", $"[OtherViewModel] Trigger key parsed: {tk}");
            }

            if (Enum.TryParse<VirtualKey>(ClickKey, out var ck))
            {
                _autoClickerService.ClickKey = ck;
                DebugLog.Info("OtherViewModel.Settings", $"[OtherViewModel] Click key parsed: {ck}");
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
                StatusMessage = "AutoClicker_MouseRequiresStopKey".GetLocalized();
                _ = SaveSettingsAsync();
            }
            else
            {
                _autoClickerService.IsEnabled = IsAutoClickerEnabled;
            }

            DebugLog.Info("OtherViewModel.Settings",
                $"[OtherViewModel] Final config - Enabled: {IsAutoClickerEnabled}, Mode: {GetCurrentMode()}, TriggerKey: {TriggerKey}, ClickKey: {ClickKey}, StopKey: {StopKey}");
        }
        catch (Exception ex)
        {
            _isInitializing = false;
            DebugLog.Info("OtherViewModel.Settings", $"[OtherViewModel] Failed to load config: {ex.Message}");
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

            DebugLog.Info("OtherViewModel.Settings",
                $"[AutoClicker] Config saved - Enabled: {IsAutoClickerEnabled}, Mode: {GetCurrentMode()}, TriggerKey: {TriggerKey}, ClickKey: {ClickKey}, StopKey: {StopKey}");

            _ = Task.Delay(2000).ContinueWith(_ =>
                _dispatcherQueue?.TryEnqueue(() => StatusMessage = string.Empty));
            AdditionalProgramPath = cleanPath;
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("Common_SaveFailed_Format".GetLocalized(), ex.Message);
            DebugLog.Info("OtherViewModel.Settings", $"[AutoClicker] Failed to save config: {ex.Message}");
        }
    }

    #endregion
}