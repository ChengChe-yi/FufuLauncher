/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Services.Backpack;

internal static class GfxLoader
{
    private static readonly HttpClient _client = new(
        new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private static readonly BitmapImage _placeholder = new(
        new Uri("ms-appx:///Assets/Backpack/Quality/UI_ItemIcon_None.png"));

    private static readonly ConcurrentDictionary<string, WeakReference<BitmapImage>> _cache = new();
    private static readonly ConcurrentDictionary<string, Task<string?>> _inflight = new();
    private static readonly string _cacheDir = Path.Combine(AppPaths.CacheDir, "Backpack", "icons");
    private static readonly SemaphoreSlim _netSem = new(8, 8);

    internal static BitmapImage Placeholder => _placeholder;

    private static string Key(Uri uri)
    {
        var s = uri.Segments;
        return s.Length >= 2 ? s[^2].TrimEnd('/') + "/" + s[^1] : s[^1];
    }

    private static void Log(string msg)
    {
        try
        {
            var logDir = Path.Combine(AppPaths.DataDir, "Backpack", "logs");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, "gfx_err.txt"), $"{DateTime.Now:HH:mm:ss.fff} {msg}\n");
        }
        catch
        {
            Debug.WriteLine($"[Backpack.Gfx] {msg}");
        }
    }

    internal static Task WarmupAsync()
    {
        Directory.CreateDirectory(_cacheDir);
        return Task.CompletedTask;
    }

    internal static Task<BitmapImage> GetAsync(Uri uri, int decodePixelWidth = 0, int decodePixelHeight = 0,
        CancellationToken cancellationToken = default) =>
        LoadAsync(uri, Math.Max(decodePixelWidth, decodePixelHeight), cancellationToken);

    internal static void BeginLoad(Uri uri, IIconUpdatable target, int decodePixelWidth = 0) =>
        _ = LoadAndSetAsync(uri, target, decodePixelWidth);

    private static async Task LoadAndSetAsync(Uri uri, IIconUpdatable target, int decodePixelWidth)
    {
        try
        {
            target.IconSource = await LoadAsync(uri, decodePixelWidth, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Backpack.Gfx] Deferred load failed: {ex.Message}");
        }
    }

    internal static async Task<BitmapImage> LoadAsync(Uri uri, int decodePixelWidth, CancellationToken cancellationToken)
    {
        var cacheKey = Key(uri);
        if (_cache.TryGetValue(cacheKey, out var weak) && weak.TryGetTarget(out var cached))
            return cached;

        var disk = await _inflight.GetOrAdd(cacheKey, _ => DownloadAsync(uri, cacheKey)).WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (disk is null)
            return _placeholder;

        if (_cache.TryGetValue(cacheKey, out weak) && weak.TryGetTarget(out cached))
            return cached;

        var bitmap = new BitmapImage();
        if (decodePixelWidth > 0)
            bitmap.DecodePixelWidth = decodePixelWidth;
        bitmap.UriSource = new Uri(disk);
        _cache[cacheKey] = new WeakReference<BitmapImage>(bitmap);

        if (_cache.Count > 512)
            TrimDeadEntries();

        return bitmap;
    }

    private static void TrimDeadEntries()
    {
        foreach (var pair in _cache)
        {
            if (!pair.Value.TryGetTarget(out _))
                _cache.TryRemove(pair.Key, out _);
        }
    }

    private static async Task<string?> DownloadAsync(Uri uri, string key)
    {
        try
        {
            var disk = Path.Combine(_cacheDir, key.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(disk)) return disk;

            await _netSem.WaitAsync().ConfigureAwait(false);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.UserAgent.ParseAdd("FufuLauncher/Backpack");
                using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Log($"HTTP {(int)response.StatusCode} {key}");
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                Directory.CreateDirectory(Path.GetDirectoryName(disk)!);
                await File.WriteAllBytesAsync(disk, bytes).ConfigureAwait(false);
                return disk;
            }
            catch (Exception ex)
            {
                Log($"NET {ex.GetType().Name}: {ex.Message} | {key}");
                return null;
            }
            finally
            {
                _netSem.Release();
            }
        }
        finally
        {
            _inflight.TryRemove(key, out _);
        }
    }
}
