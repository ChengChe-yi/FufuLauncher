/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Animation;

namespace FufuLauncher.Views;

public class StringToInitialConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string name && !string.IsNullOrEmpty(name))
        {
            return name.Substring(0, 1).ToUpper();
        }
        return "?";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed partial class BlankPage : Page
{
    #region 字段与构造函数

    private GameConfigData? _currentConfig;
    private readonly string _accountsFilePath;
    private readonly ILocalSettingsService _localSettingsService;
    private Storyboard? _redeemExpandStoryboard;
    private bool _redeemCodesExpanded;

    private TextBox? _currentEditBox;
    private TextBlock? _currentTextBlock;
    private StackPanel? _currentStackPanel;
    private GameAccountData? _currentAccount;

    public BlankPage()
    {
        InitializeComponent();
        _localSettingsService = App.GetService<ILocalSettingsService>();

        _accountsFilePath = Helpers.AppPaths.GameAccountsFile;

        Loaded += BlankPage_Loaded;
    }

    #endregion

    #region 页面生命周期与共享辅助

    private async void BlankPage_Loaded(object sender, RoutedEventArgs e)
    {
        EntranceStoryboard.Begin();
        Debug.WriteLine("========== [Debug] BlankPage_Loaded 开始 ==========");

        try
        {
            var fpsSettingObj = await _localSettingsService.ReadSettingAsync("IsFpsOverlayEnabled");
            if (fpsSettingObj is bool isFpsEnabled)
            {
                FpsOverlayToggle.Toggled -= FpsOverlayToggle_Toggled;
                FpsOverlayToggle.IsOn = isFpsEnabled;
                FpsOverlayToggle.Toggled += FpsOverlayToggle_Toggled;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Debug] 读取帧数显示开关状态失败: {ex.Message}");
        }

        try
        {
            var savedPathObj = await _localSettingsService.ReadSettingAsync("GameInstallationPath");
            var savedPath = savedPathObj as string;

            Debug.WriteLine($"[Debug] 读取到的本地保存路径: '{savedPath}'");
            Debug.WriteLine($"[Debug] IsNullOrWhiteSpace 判断结果: {string.IsNullOrWhiteSpace(savedPath)}");

            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                Debug.WriteLine("[Debug] 路径非空，跳过自动检测，直接加载已有路径。");
                savedPath = savedPath.Trim('"').Trim();
                PathTextBox.Text = savedPath;
                await LoadGameInfoAsync(savedPath);
            }
            else
            {
                Debug.WriteLine("[Debug] 路径为空，准备调用 GamePathFinder.FindGamePath()...");
                var foundPath = await GamePathFinder.FindGamePathAsync();
                Debug.WriteLine($"[Debug] GamePathFinder 返回的路径为: '{foundPath}'");

                if (!string.IsNullOrEmpty(foundPath))
                {
                    Debug.WriteLine("[Debug] 进入 DispatcherQueue，准备调用 ShowAutoPathDialog");
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await ShowAutoPathDialog(foundPath);
                    });
                }
                else
                {
                    Debug.WriteLine("[Debug] 未找到路径，跳过弹窗。");
                }
            }

            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Debug] BlankPage_Loaded 发生异常: {ex.Message}\n{ex.StackTrace}");
        }
        await LoadRedeemCodesAsync();
        Debug.WriteLine("========== [Debug] BlankPage_Loaded 结束 ==========");
    }

    private async void FpsOverlayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (FpsOverlayToggle.IsOn)
        {
            if (!FpsOverlayService.Instance.IsAdministrator())
            {
                FpsOverlayToggle.IsOn = false;
                await ShowError("Err_FpsMonitorNeedsAdmin".GetLocalized());
                return;
            }

            await _localSettingsService.SaveSettingAsync("IsFpsOverlayEnabled", true);
        }
        else
        {
            FpsOverlayService.Instance.StopOverlay();
            await _localSettingsService.SaveSettingAsync("IsFpsOverlayEnabled", false);
        }
    }

    private async Task ShowError(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Title_OperationFailed".GetLocalized(),
            Content = message,
            CloseButtonText = "OkBtn".GetLocalized(),
            XamlRoot = this.XamlRoot
        };
        await dialog.ShowAsync();
    }

    #endregion
}
