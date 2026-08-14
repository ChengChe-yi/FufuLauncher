/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Contracts.Services;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

public sealed partial class PluginPage
{
    #region 页面加载与安全提示

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        EntranceStoryboard.Begin();
    
        if (ViewModel.Plugins.Count == 0) 
        {
            ViewModel.LoadPlugins();
        }

        try
        {
            var localSettingsService = App.GetService<ILocalSettingsService>();
            var hasShownRaw = await localSettingsService.ReadSettingAsync(LocalSettingsService.HasShownSecurityWarningKey);
        
            bool hasShown = hasShownRaw is bool b && b;

            if (!hasShown)
            {
                await ShowSecurityWarningDialog();
                await localSettingsService.SaveSettingAsync(LocalSettingsService.HasShownSecurityWarningKey, true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"读取或保存安全警告配置失败: {ex.Message}");
        }
    }
    
    private async Task ShowSecurityWarningDialog()
    {
        if (XamlRoot == null) return;
    
        var textBlock = new TextBlock
        {
            Text = "安全软件会阻塞该程序的正常注入运行，如无法使用或者插件消失，请关闭你电脑的安全中心！",
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16,
            Margin = new Thickness(0, 10, 0, 0)
        };
    
        var dialog = new ContentDialog
        {
            Title = "警告",
            Content = textBlock,
            CloseButtonText = "我知道了",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
    
        await dialog.ShowAsync();
    }

    #endregion
}
