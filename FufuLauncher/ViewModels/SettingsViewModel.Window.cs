/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using FufuLauncher.Messages;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 窗口行为与通知

    partial void OnIsHamburgerButtonEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsHamburgerButtonEnabled", value);
        WeakReferenceMessenger.Default.Send(new HamburgerButtonVisibilityChangedMessage(value));
    }

    partial void OnCurrentWindowBackdropChanged(WindowBackdropType value)
    {
        Debug.WriteLine($"[ViewModel] 属性已更新为: {value}");

        if (!_isInitializing)
        {
            _localSettingsService.SaveSettingAsync("WindowBackdrop", (int)value);

            WeakReferenceMessenger.Default.Send(new ValueChangedMessage<WindowBackdropType>(value));
        }
    }

    partial void OnNotificationPositionChanged(NotificationPosition value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("NotificationPosition", (int)value);
        WeakReferenceMessenger.Default.Send(new ValueChangedMessage<NotificationPosition>(value));
    }

    partial void OnIsSaveWindowSizeEnabledChanged(bool value)
    {
        Debug.WriteLine($"SettingsViewModel: 保存窗口大小记忆设置 {value}");
        _ = _localSettingsService.SaveSettingAsync("IsSaveWindowSizeEnabled", value);
    }

    partial void OnIsMinWindowSizeLimitEnabledChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("IsMinWindowSizeLimitEnabled", value);
        WeakReferenceMessenger.Default.Send(new MinWindowSizeLimitChangedMessage(value));
    }

    partial void OnMinimizeToTrayChanged(bool value)
    {
        Debug.WriteLine($"SettingsViewModel: 保存托盘设置 {value}");
        _ = _localSettingsService.SaveSettingAsync("MinimizeToTray", value);
        WeakReferenceMessenger.Default.Send(new MinimizeToTrayChangedMessage(value));
    }

    #endregion
}
