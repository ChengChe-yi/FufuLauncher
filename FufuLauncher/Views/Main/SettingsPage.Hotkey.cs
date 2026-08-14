/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    #region 截图快捷键录制

    private void OnScreenshotHotkeyButtonClick(object sender, RoutedEventArgs e)
    {
        if (_isRecordingHotkey) return;
        _isRecordingHotkey = true;
        HotkeyRecordingHint.Visibility = Visibility.Visible;
        ScreenshotHotkeyButton.Content = "...";
        ScreenshotHotkeyButton.KeyDown += OnHotkeyRecordKeyDown;
        ScreenshotHotkeyButton.Focus(FocusState.Programmatic);
    }

    private void OnHotkeyRecordKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var key = e.Key;

        // 忽略单独的修饰键按下
        if (key == Windows.System.VirtualKey.Control ||
            key == Windows.System.VirtualKey.Shift ||
            key == Windows.System.VirtualKey.Menu ||
            key == Windows.System.VirtualKey.LeftControl ||
            key == Windows.System.VirtualKey.RightControl ||
            key == Windows.System.VirtualKey.LeftShift ||
            key == Windows.System.VirtualKey.RightShift ||
            key == Windows.System.VirtualKey.LeftMenu ||
            key == Windows.System.VirtualKey.RightMenu)
        {
            return;
        }

        var parts = new System.Collections.Generic.List<string>();

        var ctrlState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        var shiftState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Shift);
        var altState = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(Windows.System.VirtualKey.Menu);

        if (ctrlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            parts.Add("Ctrl");
        if (altState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            parts.Add("Alt");
        if (shiftState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
            parts.Add("Shift");

        parts.Add(key.ToString());

        var hotkeyStr = string.Join("+", parts);
        ViewModel.ScreenshotHotkey = hotkeyStr;
        ScreenshotHotkeyButton.Content = hotkeyStr;

        _isRecordingHotkey = false;
        HotkeyRecordingHint.Visibility = Visibility.Collapsed;
        ScreenshotHotkeyButton.KeyDown -= OnHotkeyRecordKeyDown;
        e.Handled = true;
    }

    #endregion
}
