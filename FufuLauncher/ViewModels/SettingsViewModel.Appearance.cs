/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 外观与样式

    private async Task ResetLaunchButtonOverlayColorAsync()
    {
        LaunchButtonOverlayColor = "#0078D7";
        await _localSettingsService.SaveSettingAsync("LaunchButtonOverlayColor", "#0078D7");
        WeakReferenceMessenger.Default.Send(new FufuLauncher.Messages.TextStyleChangedMessage());
    }

    partial void OnLaunchButtonOverlayColorChanged(string value)
    {
        _localSettingsService.SaveSettingAsync("LaunchButtonOverlayColor", value);
        WeakReferenceMessenger.Default.Send(new FufuLauncher.Messages.TextStyleChangedMessage());
    }

    partial void OnIsAcrylicOverlayEnabledChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("IsAcrylicOverlayEnabled", value);
        WeakReferenceMessenger.Default.Send(new OverlayStyleChangedMessage(value));
    }

    partial void OnIsPageOverlaySemiTransparentEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsPageOverlaySemiTransparentEnabled", value);
        WeakReferenceMessenger.Default.Send(new PageOverlayOpacityModeChangedMessage(value));
    }

    partial void OnPageOverlayTargetOpacityChanged(double value)
    {
        if (_isInitializing) return;
        var clamped = Math.Clamp(value, 0.1, 1.0);
        if (Math.Abs(clamped - value) > 0.0001)
        {
            PageOverlayTargetOpacity = clamped;
            return;
        }
        _ = _localSettingsService.SaveSettingAsync("PageOverlayTargetOpacity", clamped);
        WeakReferenceMessenger.Default.Send(new PageOverlayTargetOpacityChangedMessage(clamped));
    }

    partial void OnAppThemeColorChanged(string value)
    {
        _localSettingsService.SaveSettingAsync("AppThemeColor", value);
        
        try
        {
            if (!string.IsNullOrEmpty(value) && value.StartsWith("#") && (value.Length == 7 || value.Length == 9))
            {
                string hex = value.Replace("#", "");
                byte a = 255;
                byte r = 0;
                byte g = 0;
                byte b = 0;
                if (hex.Length == 8)
                {
                    a = Convert.ToByte(hex.Substring(0, 2), 16);
                    r = Convert.ToByte(hex.Substring(2, 2), 16);
                    g = Convert.ToByte(hex.Substring(4, 2), 16);
                    b = Convert.ToByte(hex.Substring(6, 2), 16);
                }
                else if (hex.Length == 6)
                {
                    r = Convert.ToByte(hex.Substring(0, 2), 16);
                    g = Convert.ToByte(hex.Substring(2, 2), 16);
                    b = Convert.ToByte(hex.Substring(4, 2), 16);
                }
                var color = Windows.UI.Color.FromArgb(a, r, g, b);
                if (_appThemeColorObj != color)
                {
                    _appThemeColorObj = color;
                    OnPropertyChanged(nameof(AppThemeColorObj));
                }
            }
        }
        catch { }

        WeakReferenceMessenger.Default.Send(new AcrylicSettingChangedMessage(true)); // reuse or create new msg
        ThemeHelper.ApplyThemeColor(value);
    }

    partial void OnAppThemeColorObjChanged(Windows.UI.Color value)
    {
        string hex = $"#{value.A:X2}{value.R:X2}{value.G:X2}{value.B:X2}";
        if (AppThemeColor != hex)
        {
            AppThemeColor = hex;
        }
    }

    partial void OnGlobalBackgroundImageOpacityChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        if (Math.Abs(clamped - value) > 0.0001)
        {
            GlobalBackgroundImageOpacity = clamped;
            return;
        }

        _ = _localSettingsService.SaveSettingAsync("GlobalBackgroundImageOpacity", clamped);
        WeakReferenceMessenger.Default.Send(new BackgroundImageOpacityChangedMessage(clamped));
    }

    partial void OnPanelBackgroundOpacityChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);

        if (Math.Abs(clamped - value) > 0.001)
        {
            PanelBackgroundOpacity = clamped;
            return;
        }

        _localSettingsService.SaveSettingAsync("PanelBackgroundOpacity", clamped);

        WeakReferenceMessenger.Default.Send(new PanelOpacityChangedMessage(clamped));
    }

    partial void OnGameNewsCardTextColorChanged(string value)
    {
        _ = _localSettingsService.SaveSettingAsync("GameNewsCardTextColor", value);
        WeakReferenceMessenger.Default.Send(new FufuLauncher.Messages.TextStyleChangedMessage());
    }
    partial void OnGameNewsCardTextOpacityChanged(double value)
    {
        _ = _localSettingsService.SaveSettingAsync("GameNewsCardTextOpacity", value);
        WeakReferenceMessenger.Default.Send(new FufuLauncher.Messages.TextStyleChangedMessage());
    }
    partial void OnLaunchButtonTextColorChanged(string value)
    {
        _ = _localSettingsService.SaveSettingAsync("LaunchButtonTextColor", value);
        WeakReferenceMessenger.Default.Send(new FufuLauncher.Messages.TextStyleChangedMessage());
    }
    partial void OnLaunchButtonTextOpacityChanged(double value)
    {
        _ = _localSettingsService.SaveSettingAsync("LaunchButtonTextOpacity", value);
        WeakReferenceMessenger.Default.Send(new FufuLauncher.Messages.TextStyleChangedMessage());
    }
    partial void OnGameCheckinTextColorChanged(string value)
    {
        _ = _localSettingsService.SaveSettingAsync("GameCheckinTextColor", value);
        WeakReferenceMessenger.Default.Send(new FufuLauncher.Messages.TextStyleChangedMessage());
    }
    partial void OnGameCheckinTextOpacityChanged(double value)
    {
        _ = _localSettingsService.SaveSettingAsync("GameCheckinTextOpacity", value);
        WeakReferenceMessenger.Default.Send(new FufuLauncher.Messages.TextStyleChangedMessage());
    }

    partial void OnGlobalBackgroundOverlayOpacityChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);

        if (Math.Abs(clamped - value) > 0.0001)
        {
            GlobalBackgroundOverlayOpacity = clamped;
            return;
        }

        _ = _localSettingsService.SaveSettingAsync("GlobalBackgroundOverlayOpacity", clamped);
        WeakReferenceMessenger.Default.Send(new BackgroundOverlayOpacityChangedMessage(clamped));
    }

    partial void OnContentFrameBackgroundOpacityChanged(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        if (Math.Abs(clamped - value) > 0.0001)
        {
            ContentFrameBackgroundOpacity = clamped;
            return;
        }

        _ = _localSettingsService.SaveSettingAsync("ContentFrameBackgroundOpacity", clamped);
        WeakReferenceMessenger.Default.Send(new FrameBackgroundOpacityChangedMessage(clamped));
    }

    #endregion
}
