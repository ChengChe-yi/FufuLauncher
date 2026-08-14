/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json.Nodes;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using FufuLauncher.Services;

namespace FufuLauncher.Views;

public sealed partial class BBSWindow
{
    #region 截图分享与保存

    private byte[] _screenshotBytes;

    private async Task<JsResult?> HandleShareAsync(JsParam param)
    {
        string type = param.Payload?["type"]?.ToString();
        if (type == "screenshot")
        {
            try
            {
                string resultJson = await BBSWebView.CoreWebView2.CallDevToolsProtocolMethodAsync("Page.captureScreenshot", """{"format":"png","captureBeyondViewport":true}""");
                var node = JsonNode.Parse(resultJson);
                string base64 = node?["data"]?.ToString();
                if (!string.IsNullOrEmpty(base64)) await ShowScreenshotAsync(base64);
            }
            catch { }
        }
        else if (type == "image")
        {
            string base64 = param.Payload?["content"]?["image_base64"]?.ToString();
            if (!string.IsNullOrEmpty(base64)) await ShowScreenshotAsync(base64);
        }
        return new JsResult { Data = new() { ["type"] = type } };
    }

    private async Task ShowScreenshotAsync(string base64)
    {
        try
        {
            _screenshotBytes = Convert.FromBase64String(base64);
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(_screenshotBytes.AsBuffer());
            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            ScreenshotImage.Source = bitmap;
            ScreenshotGrid.Visibility = Visibility.Visible;
        }
        catch { }
    }

    private async void SaveScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (_screenshotBytes == null) return;
        try
        {
            var path = await FilePickerService.PickSaveFileAsync(
                this,
                new[] { ("PNG Image", new[] { ".png" }) },
                $"mihoyo_bbs_{DateTime.Now:yyyyMMddHHmmss}",
                PickerLocationId.PicturesLibrary);
            if (!string.IsNullOrEmpty(path))
            {
                await File.WriteAllBytesAsync(path, _screenshotBytes);
                CloseScreenshot_Click(null, null);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BBSWindow] 保存截图失败: {ex.Message}");
        }
    }

    private async void CopyScreenshot_Click(object sender, RoutedEventArgs e)
    {
        if (_screenshotBytes == null) return;
        try
        {
            var dataPackage = new DataPackage { RequestedOperation = DataPackageOperation.Copy };
            using var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(_screenshotBytes.AsBuffer());
            stream.Seek(0);
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            Clipboard.SetContent(dataPackage);
            CloseScreenshot_Click(null, null);
        }
        catch { }
    }

    private void CloseScreenshot_Click(object sender, RoutedEventArgs e)
    {
        ScreenshotGrid.Visibility = Visibility.Collapsed;
        _screenshotBytes = null;
        ScreenshotImage.Source = null;
    }

    #endregion
}
