/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class PluginPage
{
    #region 对话框交互

    private async void ViewModel_DuplicateDetected(object? sender, string message)
    {
        await Task.Delay(1000);
        
        DispatcherQueue.TryEnqueue(async () => 
        {
            if (XamlRoot == null || !IsLoaded) return;

            await ShowDuplicateDialog(message);
        });
    }
    
    private async void OnFreeCamHelpClick(object sender, RoutedEventArgs e)
    {
        try
        {
            string imagePath = Path.Combine(AppContext.BaseDirectory, "Assets", "freecam.png");

            if (File.Exists(imagePath))
            {
                var bitmap = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(imagePath));
                var image = new Image
                {
                    Source = bitmap,
                    Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform
                };

                var dialog = new ContentDialog
                {
                    Title = "自由视角使用说明",
                    Content = image,
                    CloseButtonText = "关闭",
                    XamlRoot = XamlRoot,
                    Resources = { ["ContentDialogMaxWidth"] = 900.0 }
                };
                await dialog.ShowAsync();
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = "文件未找到",
                    Content = "未能在 Assets 文件夹中找到 freecam.png",
                    CloseButtonText = "确定",
                    XamlRoot = XamlRoot
                };
                await dialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            ViewModel.StatusMessage = $"无法打开说明图: {ex.Message}";
        }
    }
    
    private async Task ShowDuplicateDialog(string message)
    {
        if (XamlRoot == null) return;

        var dialog = new ContentDialog
        {
            Title = "插件冲突警告",
            Content = new ScrollViewer 
            { 
                Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                MaxHeight = 300
            },
            PrimaryButtonText = "打开插件目录",
            CloseButtonText = "忽略",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            ViewModel.OpenFolderCommand.Execute(null);
        }
    }

    #endregion
}
