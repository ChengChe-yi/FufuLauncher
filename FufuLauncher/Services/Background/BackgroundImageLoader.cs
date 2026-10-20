/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using FufuLauncher.Models;

namespace FufuLauncher.Services.Background;

internal static class BackgroundImageLoader
{
    public static Task<ImageSource> LoadFileAsync(string path) => LoadAsync(() => File.OpenRead(path));

    public static async Task<ImageSource> LoadFallbackAsync()
    {
        try
        {
            return await LoadFileAsync(Path.Combine(AppContext.BaseDirectory, "Assets", "bg.png"));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"BackgroundImageLoader: 默认背景文件不可用，使用内嵌背景: {ex.Message}");
        }

        return await LoadAsync(() => typeof(BackgroundImageLoader).Assembly.GetManifestResourceStream(
            "FufuLauncher.Assets.bg.png") ?? throw new FileNotFoundException("内嵌默认背景不存在"));
    }

    private static async Task<ImageSource> LoadAsync(Func<Stream> openStream)
    {
        var dispatcher = App.MainWindow?.DispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
        if (dispatcher == null)
            throw new InvalidOperationException("背景图片加载需要主窗口的 UI 线程");


        var decoded = await Task.Run(async () =>
        {
            var quality = await BackgroundImageQualitySettings.GetEffectiveQualityAsync().ConfigureAwait(false);
            using var stream = openStream();
            using var image = Image.Load<Bgra32>(new DecoderOptions { MaxFrames = 1 }, stream);
            image.Mutate(context => context.AutoOrient());
            ApplyDisplayQuality(image, quality);
            var pixels = new byte[checked(image.Width * image.Height * 4)];
            image.CopyPixelDataTo(pixels);


            for (var i = 0; i < pixels.Length; i += 4)
            {
                var alpha = pixels[i + 3];
                if (alpha == 255) continue;
                pixels[i] = (byte)((pixels[i] * alpha + 127) / 255);
                pixels[i + 1] = (byte)((pixels[i + 1] * alpha + 127) / 255);
                pixels[i + 2] = (byte)((pixels[i + 2] * alpha + 127) / 255);
            }

            return (image.Width, image.Height, Pixels: pixels);
        }).ConfigureAwait(false);

        var completion = new TaskCompletionSource<ImageSource>(TaskCreationOptions.RunContinuationsAsynchronously);

        void CreateBitmap()
        {
            try
            {
                var bitmap = new WriteableBitmap(decoded.Width, decoded.Height);
                using var buffer = bitmap.PixelBuffer.AsStream();
                buffer.Write(decoded.Pixels, 0, decoded.Pixels.Length);
                bitmap.Invalidate();
                completion.SetResult(bitmap);
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        }

        if (dispatcher.HasThreadAccess)
            CreateBitmap();
        else if (!dispatcher.TryEnqueue(CreateBitmap))
            completion.SetException(new InvalidOperationException("背景图片的 UI 调度队列已关闭"));

        return await completion.Task.ConfigureAwait(false);
    }

    private static void ApplyDisplayQuality(Image<Bgra32> image, BackgroundImageQuality quality)
    {
        var maxEdge = BackgroundImageQualitySettings.GetMaxDecodeEdge(quality);
        if (maxEdge <= 0)
            return;

        var longestEdge = Math.Max(image.Width, image.Height);
        if (longestEdge <= maxEdge)
            return;

        var scale = (double)maxEdge / longestEdge;
        var width = Math.Max(1, (int)Math.Round(image.Width * scale));
        var height = Math.Max(1, (int)Math.Round(image.Height * scale));
        image.Mutate(context => context.Resize(width, height));
    }
}