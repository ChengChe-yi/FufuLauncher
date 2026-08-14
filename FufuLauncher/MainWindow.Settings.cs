/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher;

public sealed partial class MainWindow
{
    #region Opacity & Visual Settings

    private async Task LoadOverlayOpacityAsync()
    {
        try
        {
            var valueObj = await _localSettingsService.ReadSettingAsync("GlobalBackgroundOverlayOpacity");
            var opacity = 0.0;
            if (valueObj != null && double.TryParse(valueObj.ToString(), out var parsed)) opacity = parsed;
            ApplyOverlayOpacity(opacity);
        }
        catch { ApplyOverlayOpacity(0.0); }
    }

    private async Task LoadFrameBackgroundOpacityAsync()
    {
        try
        {
            var valueObj = await _localSettingsService.ReadSettingAsync("ContentFrameBackgroundOpacity");
            var opacity = 0.0;
            if (valueObj != null && double.TryParse(valueObj.ToString(), out var parsed)) opacity = parsed;
            ApplyFrameBackgroundOpacity(opacity);
        }
        catch { ApplyFrameBackgroundOpacity(0.0); }
    }

    private void ApplyOverlayOpacity(double value)
    {
        GlobalBackgroundOverlay.Opacity = Math.Clamp(value, 0.0, 1.0);
    }

    private void ApplyFrameBackgroundOpacity(double value)
    {
        _frameBackgroundOpacity = Math.Clamp(value, 0.0, 1.0);
        if (ContentFrame == null) return;

        if (_frameBackgroundOpacity < 0.05)
        {
            ContentFrame.Background = new SolidColorBrush(Colors.Transparent);
            return;
        }

        SolidColorBrush brush;
        if (ContentFrame.Background is SolidColorBrush existingBrush) brush = existingBrush;
        else { brush = new SolidColorBrush(); ContentFrame.Background = brush; }

        var theme = ElementTheme.Default;
        if (Content is FrameworkElement root)
        {
            theme = root.ActualTheme;
            if (theme == ElementTheme.Default) theme = Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
        }
        var baseColor = theme == ElementTheme.Dark ? Colors.Black : Colors.White;
        baseColor.A = (byte)(_frameBackgroundOpacity * 255);
        brush.Color = baseColor;
    }

    private async Task LoadAcrylicOverlaySettingAsync()
    {
        try
        {
            var valueObj = await _localSettingsService.ReadSettingAsync("IsAcrylicOverlayEnabled");
            _isAcrylicOverlayEnabled = valueObj != null && Convert.ToBoolean(valueObj);
            UpdateBackgroundOverlayTheme();
        }
        catch { _isAcrylicOverlayEnabled = false; }
    }

    private async Task LoadPageOverlayOpacitySettingAsync()
    {
        try
        {
            var modeObj = await _localSettingsService.ReadSettingAsync("IsPageOverlaySemiTransparentEnabled");
            _isPageOverlaySemiTransparent = modeObj != null && Convert.ToBoolean(modeObj);

            var opacityObj = await _localSettingsService.ReadSettingAsync("PageOverlayTargetOpacity");
            if (opacityObj != null && double.TryParse(opacityObj.ToString(), out var parsed))
                _pageOverlayTargetOpacity = Math.Clamp(parsed, 0.1, 1.0);
            else
                _pageOverlayTargetOpacity = 0.7;
        }
        catch
        {
            _isPageOverlaySemiTransparent = false;
            _pageOverlayTargetOpacity = 0.7;
        }
    }

    #endregion
}
