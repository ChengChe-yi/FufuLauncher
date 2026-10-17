using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Services;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using DisplayAdvancedColorKind = Microsoft.Graphics.Display.DisplayAdvancedColorKind;
using DisplayInformation = Microsoft.Graphics.Display.DisplayInformation;

namespace FufuLauncher.Views;

[INotifyPropertyChanged]
public sealed partial class GenshinHDRSettingPanel : UserControl
{
    private DisplayInformation? _displayInformation;
    private Flyout? _hostFlyout;

    public GenshinHDRSettingPanel()
    {
        InitializeComponent();
        Loaded += GenshinHDRSettingPanel_Loaded;
        Unloaded += GenshinHDRSettingPanel_Unloaded;
    }

    public static void ShowAt(FrameworkElement target, FlyoutPlacementMode placement = FlyoutPlacementMode.Bottom)
    {
        var panel = new GenshinHDRSettingPanel();
        var flyout = new Flyout
        {
            Content = panel,
            Placement = placement,
            ShouldConstrainToRootBounds = false,
            AreOpenCloseAnimationsEnabled = true
        };
        flyout.FlyoutPresenterStyle = new Style(typeof(FlyoutPresenter))
        {
            Setters =
            {
                new Setter(Control.PaddingProperty, new Thickness(0)),
                new Setter(Control.CornerRadiusProperty, new CornerRadius(12)),
                new Setter(FrameworkElement.MaxWidthProperty, 500.0),
                new Setter(FrameworkElement.MaxHeightProperty, 650.0),
                new Setter(Control.BorderThicknessProperty, new Thickness(1))
            }
        };
        panel._hostFlyout = flyout;
        flyout.ShowAt(target);
    }

    private async void GenshinHDRSettingPanel_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            XamlRoot.Changed -= XamlRoot_Changed;
            XamlRoot.Changed += XamlRoot_Changed;

            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);

            _displayInformation = DisplayInformation.CreateForWindowId(windowId);

            if (_displayInformation != null)
            {
                _displayInformation.AdvancedColorInfoChanged += _displayInformation_AdvancedColorInfoChanged;
                UpdateDisplayInfomation(_displayInformation);
            }

            (MaxLuminance, SceneLuminance, UILuminance) = GameSettingService.GetGenshinHDRLuminance();
            var localSettings = App.GetService<ILocalSettingsService>();
            if (localSettings != null)
            {
                var val = await localSettings.ReadSettingAsync(GameLauncherService.GenshinHDRConfigKey);
                if (val != null)
                {
                    IsGenshinHDRForceEnabled = Convert.ToBoolean(val);
                }
                else
                {
                    IsGenshinHDRForceEnabled = GameSettingService.GetGenshinHDRState();
                }
            }
            else
            {
                IsGenshinHDRForceEnabled = GameSettingService.GetGenshinHDRState();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        if (_displayInformation != null)
        {
            UpdateDisplayInfomation(_displayInformation);
        }
    }

    private void GenshinHDRSettingPanel_Unloaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_displayInformation != null)
            {
                _displayInformation.AdvancedColorInfoChanged -= _displayInformation_AdvancedColorInfoChanged;
            }

            if (XamlRoot != null)
            {
                XamlRoot.Changed -= XamlRoot_Changed;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    [ObservableProperty] public partial bool HDRNotSupported { get; set; }
    [ObservableProperty] public partial bool HDRNotEnabled { get; set; }
    [ObservableProperty] public partial bool HDREnabled { get; set; }
    [ObservableProperty] public partial string DisplayColorSpace { get; set; } = "检测中...";
    [ObservableProperty] public partial string DisplayMaxLuminance { get; set; } = "-";
    [ObservableProperty] public partial string DisplayFullFrameLuminance { get; set; } = "-";
    [ObservableProperty] public partial string DisplaySdrWhiteLevel { get; set; } = "-";
    [ObservableProperty] public partial string DisplayInfomation { get; set; } = "";
    [ObservableProperty] public partial int MaxLuminance { get; set; } = 1000;
    [ObservableProperty] public partial int SceneLuminance { get; set; } = 300;
    [ObservableProperty] public partial int UILuminance { get; set; } = 350;
    [ObservableProperty] public partial bool IsGenshinHDRForceEnabled { get; set; }
    [ObservableProperty] public partial bool IsSaveSuccessVisible { get; set; }

    [ObservableProperty] public partial Brush StatusBadgeBackground { get; set; } = new SolidColorBrush(Color.FromArgb(24, 16, 185, 129));
    [ObservableProperty] public partial Brush StatusBadgeBorder { get; set; } = new SolidColorBrush(Color.FromArgb(64, 16, 185, 129));
    [ObservableProperty] public partial Brush StatusBadgeForeground { get; set; } = new SolidColorBrush(Color.FromArgb(255, 16, 185, 129));
    [ObservableProperty] public partial string StatusBadgeText { get; set; } = "HDR 已就绪";

    private void _displayInformation_AdvancedColorInfoChanged(DisplayInformation sender, object args)
    {
        DispatcherQueue.TryEnqueue(() => UpdateDisplayInfomation(sender));
    }

    private void UpdateDisplayInfomation(DisplayInformation display)
    {
        if (display == null) return;
        try
        {
            var info = display.GetAdvancedColorInfo();
            if (info == null) return;

            HDRNotSupported = !info.IsAdvancedColorKindAvailable(DisplayAdvancedColorKind.HighDynamicRange);
            HDRNotEnabled = info.CurrentAdvancedColorKind is not DisplayAdvancedColorKind.HighDynamicRange;
            HDREnabled = !HDRNotEnabled;

            DisplayColorSpace = info.CurrentAdvancedColorKind switch
            {
                DisplayAdvancedColorKind.StandardDynamicRange => "SDR (标准动态范围)",
                DisplayAdvancedColorKind.WideColorGamut => "WCG (广色域)",
                DisplayAdvancedColorKind.HighDynamicRange => "HDR (高动态范围)",
                _ => "未知"
            };

            DisplayMaxLuminance = $"{info.MaxLuminanceInNits:F0} nits";
            DisplayFullFrameLuminance = $"{info.MaxAverageFullFrameLuminanceInNits:F0} nits";
            DisplaySdrWhiteLevel = $"{info.SdrWhiteLevelInNits:F0} nits";

            if (HDREnabled)
            {
                StatusBadgeBackground = new SolidColorBrush(Color.FromArgb(24, 16, 185, 129));
                StatusBadgeBorder = new SolidColorBrush(Color.FromArgb(64, 16, 185, 129));
                StatusBadgeForeground = new SolidColorBrush(Color.FromArgb(255, 16, 185, 129));
                StatusBadgeText = "HDR 已就绪";
            }
            else if (HDRNotSupported)
            {
                StatusBadgeBackground = new SolidColorBrush(Color.FromArgb(24, 239, 68, 68));
                StatusBadgeBorder = new SolidColorBrush(Color.FromArgb(64, 239, 68, 68));
                StatusBadgeForeground = new SolidColorBrush(Color.FromArgb(255, 239, 68, 68));
                StatusBadgeText = "不支持 HDR";
            }
            else
            {
                StatusBadgeBackground = new SolidColorBrush(Color.FromArgb(24, 245, 158, 11));
                StatusBadgeBorder = new SolidColorBrush(Color.FromArgb(64, 245, 158, 11));
                StatusBadgeForeground = new SolidColorBrush(Color.FromArgb(255, 245, 158, 11));
                StatusBadgeText = "HDR 未开启";
            }

            DisplayInfomation = $"""
                                 色彩空间: {DisplayColorSpace}
                                 峰值亮度: {DisplayMaxLuminance}
                                 最大全屏亮度: {DisplayFullFrameLuminance}
                                 SDR白色亮度: {DisplaySdrWhiteLevel}
                                 """;
        }
        catch
        {
            DisplayColorSpace = "无法读取";
            DisplayMaxLuminance = "-";
            DisplayFullFrameLuminance = "-";
            DisplaySdrWhiteLevel = "-";
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        GameSettingService.SetGenshinHDRLuminance(MaxLuminance, SceneLuminance, UILuminance);
        GameSettingService.SetGenshinHDRState(IsGenshinHDRForceEnabled);

        var localSettings = App.GetService<ILocalSettingsService>();
        if (localSettings != null)
        {
            await localSettings.SaveSettingAsync(GameLauncherService.GenshinHDRConfigKey, IsGenshinHDRForceEnabled);
        }

        IsSaveSuccessVisible = true;
        await Task.Delay(900);
        IsSaveSuccessVisible = false;
        Close();
    }

    [RelayCommand]
    private void Reset()
    {
        MaxLuminance = 1000;
        SceneLuminance = 300;
        UILuminance = 350;
        IsGenshinHDRForceEnabled = false;
    }

    [RelayCommand]
    private void AutoAdjust()
    {
        if (_displayInformation == null) return;
        try
        {
            var info = _displayInformation.GetAdvancedColorInfo();
            if (info != null)
            {
                var maxLum = info.MaxLuminanceInNits > 0 ? info.MaxLuminanceInNits : 1000;
                var sdrWhite = info.SdrWhiteLevelInNits > 0 ? info.SdrWhiteLevelInNits : 200;

                MaxLuminance = (int)Math.Clamp(maxLum, 300, 2000);
                SceneLuminance = (int)Math.Clamp(sdrWhite + 20, 100, 500);
                UILuminance = Math.Clamp(SceneLuminance + 50, 150, 550);
            }
        }
        catch
        {
        }
    }

    [RelayCommand]
    public void Close()
    {
        if (_hostFlyout != null)
        {
            _hostFlyout.Hide();
            return;
        }

        DependencyObject current = this;
        while (current != null)
        {
            if (current is Popup popup)
            {
                popup.IsOpen = false;
                return;
            }
            current = VisualTreeHelper.GetParent(current);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
