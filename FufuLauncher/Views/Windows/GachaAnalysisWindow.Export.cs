/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;

namespace FufuLauncher.Views;

public sealed partial class GachaAnalysisWindow
{
    private async Task<InMemoryRandomAccessStream> RenderElementToStreamAsync(UIElement element)
    {
        AnalysisExportButtons.Visibility = Visibility.Collapsed;
        AnalysisExportLogo.Visibility = Visibility.Visible;
        AnalysisExportTarget.Background =
            (Brush)Application.Current.Resources["ApplicationPageBackgroundThemeBrush"]
            ?? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

        var tcs = new TaskCompletionSource();
        DispatcherQueue.TryEnqueue(() => tcs.SetResult());
        await tcs.Task;

        try
        {
            var renderTargetBitmap = new RenderTargetBitmap();
            await renderTargetBitmap.RenderAsync(element);

            var pixels = await renderTargetBitmap.GetPixelsAsync();

            var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);

            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)renderTargetBitmap.PixelWidth,
                (uint)renderTargetBitmap.PixelHeight,
                96,
                96,
                pixels.ToArray());

            await encoder.FlushAsync();
            return stream;
        }
        finally
        {
            AnalysisExportButtons.Visibility = Visibility.Visible;
            AnalysisExportLogo.Visibility = Visibility.Collapsed;
            AnalysisExportTarget.Background = null;
        }
    }

    private async void ShowDialogMessage(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "知道了",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void OnCopyAnalysisImageClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var stream = await RenderElementToStreamAsync(AnalysisExportTarget);
            stream.Seek(0);
            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            Clipboard.SetContent(dataPackage);

            for (var i = 0; i < 5; i++)
            {
                try
                {
                    Clipboard.Flush();
                    break;
                }
                catch (System.Runtime.InteropServices.COMException) when (i < 4)
                {
                    await Task.Delay(100);
                }
            }

            ShowDialogMessage("复制成功", "祈愿分析图片已复制到剪贴板。");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gacha] 复制图片失败: {ex}");
            ShowDialogMessage("复制失败", ex.Message);
        }
    }

    private async void OnSaveAnalysisImageClick(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await FilePickerService.PickSaveFileAsync(
                this,
                new[] { ("PNG 图片", new[] { ".png" }) },
                $"祈愿分析_{ViewModel.SelectedUid}_{DateTime.Now:yyyyMMddHHmmss}",
                PickerLocationId.PicturesLibrary,
                msg => ShowDialogMessage("错误", msg));
            if (string.IsNullOrEmpty(path)) return;

            using var stream = await RenderElementToStreamAsync(AnalysisExportTarget);
            stream.Seek(0);
            using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
            stream.AsStream().CopyTo(fileStream);

            ShowDialogMessage("保存成功", $"祈愿分析图片已保存至文件。");
        }
        catch (Exception ex)
        {
            ShowDialogMessage("保存失败", $"详细信息: {ex.Message}");
        }
    }
}