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
    #region Auto Clicker

    private void StartRecordingTriggerKey()
    {
        IsRecordingTriggerKey = true;
        IsRecordingClickKey = false;
        IsRecordingStopKey = false;
        Debug.WriteLine("[OtherViewModel] 开始录制触发键");
    }

    private void StartRecordingClickKey()
    {
        IsRecordingClickKey = true;
        IsRecordingTriggerKey = false;
        IsRecordingStopKey = false;
        Debug.WriteLine("[OtherViewModel] 开始录制连点键");
    }

    private void StartRecordingStopKey()
    {
        IsRecordingStopKey = true;
        IsRecordingTriggerKey = false;
        IsRecordingClickKey = false;
        Debug.WriteLine("[OtherViewModel] 开始录制停止快捷键");
    }

    partial void OnIsAutoClickerEnabledChanged(bool value)
    {
        if (_isInitializing || _isReverting) return;

        if (value)
        {
            if (IsMouseModeEnabled() && !HasStopKey())
            {
                StatusMessage = "请先设置键盘停止快捷键，再开启鼠标连点";
                RevertAutoClickerToggle(false);
                RevertMouseClickerToggles();
                return;
            }

            Debug.WriteLine("[OtherViewModel] 拦截开启请求，弹出风险提示");
            _ = HandleAutoClickerEnableRequestAsync();
        }
        else
        {
            _autoClickerService.IsEnabled = false;
            _ = SaveSettingsAsync();
            Debug.WriteLine($"[OtherViewModel] 连点器启用状态切换: {value}");
        }
    }

    partial void OnIsMouseLeftClickerEnabledChanged(bool value)
    {
        if (_isInitializing || _isReverting) return;

        if (value)
        {
            _isReverting = true;
            IsMouseRightClickerEnabled = false;
            _isReverting = false;
        }

        ApplyClickerModeFromSelection();
    }

    partial void OnIsMouseRightClickerEnabledChanged(bool value)
    {
        if (_isInitializing || _isReverting) return;

        if (value)
        {
            _isReverting = true;
            IsMouseLeftClickerEnabled = false;
            _isReverting = false;
        }

        ApplyClickerModeFromSelection();
    }

    private async Task HandleAutoClickerEnableRequestAsync()
    {
        bool confirmed = await ShowLatencyWarningDialogAsync();

        if (!confirmed)
        {
            Debug.WriteLine("[OtherViewModel] 用户取消开启连点器");
            RevertAutoClickerToggle(false);
            return;
        }

        if (!IsAdministrator())
        {
            Debug.WriteLine("[OtherViewModel] 尝试启用连点器，但没有管理员权限被拦截");
            RevertAutoClickerToggle(false);
            _ = ShowAdminRequiredDialogAsync();
            return;
        }

        _autoClickerService.Mode = GetCurrentMode();
        _autoClickerService.IsEnabled = true;
        _ = SaveSettingsAsync();
        Debug.WriteLine("[OtherViewModel] 连点器启用状态切换: True");
    }

    public void UpdateKey(string keyType, VirtualKey key)
    {
        var keyStr = key.ToString();
        Debug.WriteLine($"[OtherViewModel] 更新按键 - 类型: {keyType}, 按键: {keyStr}");

        if (keyType == "Trigger")
        {
            TriggerKey = keyStr;
            _autoClickerService.TriggerKey = key;
        }
        else if (keyType == "Click")
        {
            ClickKey = keyStr;
            _autoClickerService.ClickKey = key;
        }
        else if (keyType == "Stop")
        {
            StopKey = keyStr;
            _autoClickerService.StopKey = key;
        }

        IsRecordingTriggerKey = false;
        IsRecordingClickKey = false;
        IsRecordingStopKey = false;

        _ = SaveSettingsAsync();
    }

    private void ApplyClickerModeFromSelection()
    {
        var mode = GetCurrentMode();

        if (mode != AutoClickerMode.Keyboard && !HasStopKey())
        {
            StatusMessage = "请先设置键盘停止快捷键，再开启鼠标连点";
            RevertMouseClickerToggles();
            return;
        }

        _autoClickerService.Mode = mode;
        _ = SaveSettingsAsync();
    }

    private AutoClickerMode GetCurrentMode()
    {
        if (IsMouseLeftClickerEnabled) return AutoClickerMode.MouseLeft;
        if (IsMouseRightClickerEnabled) return AutoClickerMode.MouseRight;
        return AutoClickerMode.Keyboard;
    }

    private bool IsMouseModeEnabled()
    {
        return IsMouseLeftClickerEnabled || IsMouseRightClickerEnabled;
    }

    private bool HasStopKey()
    {
        return !string.IsNullOrWhiteSpace(StopKey) && Enum.TryParse<VirtualKey>(StopKey, out var key) && key != VirtualKey.None;
    }

    private void RevertAutoClickerToggle(bool value)
    {
        _isReverting = true;
        _dispatcherQueue.TryEnqueue(() =>
        {
            IsAutoClickerEnabled = value;
            _isReverting = false;
        });
    }

    private void RevertMouseClickerToggles()
    {
        _isReverting = true;
        IsMouseLeftClickerEnabled = false;
        IsMouseRightClickerEnabled = false;
        _autoClickerService.Mode = AutoClickerMode.Keyboard;
        _isReverting = false;
        _ = SaveSettingsAsync();
    }

    private void AutoClickerService_IsEnabledChanged(object sender, bool value)
    {
        if (_isInitializing || _isReverting) return;

        _dispatcherQueue.TryEnqueue(() =>
        {
            if (IsAutoClickerEnabled == value) return;

            _isReverting = true;
            IsAutoClickerEnabled = value;
            _isReverting = false;
            if (!value)
            {
                StatusMessage = "连点器已通过停止快捷键关闭";
            }
        });
    }

    #endregion
}
