using System.Diagnostics;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services.Background;

public enum RandomBackgroundType
{
    All,
    Home,
    Cg
}

public static class RandomBackgroundService
{
    private const string Endpoint = "https://img.catcdn.cn/ba/";
    private const int MaxDownloadBytes = 20 * 1024 * 1024;
    private static readonly HttpClient Client;
    private static int _generation;

    static RandomBackgroundService()
    {
        Client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        Client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/155.0.0.0 Safari/537.36");
        Client.DefaultRequestHeaders.Accept.ParseAdd("image/webp,image/*,*/*;q=0.8");
    }

    public static string CacheDirectory => Path.Combine(AppPaths.CacheDir, "RandomBackground");

    public static RandomBackgroundType ParseType(string? value) =>
        Enum.TryParse<RandomBackgroundType>(value, true, out var type) &&
        Enum.IsDefined(typeof(RandomBackgroundType), type)
            ? type
            : RandomBackgroundType.All;

    private static string GetEndpoint(RandomBackgroundType type) => type switch
    {
        RandomBackgroundType.Home => Endpoint + "?type=home",
        RandomBackgroundType.Cg => Endpoint + "?type=cg",
        _ => Endpoint
    };

    private static string GetCacheFilePath(RandomBackgroundType type)
    {
        var fileName = type switch
        {
            RandomBackgroundType.Home => "next_home.img",
            RandomBackgroundType.Cg => "next_cg.img",
            _ => "next_all.img"
        };
        return Path.Combine(CacheDirectory, fileName);
    }

    public static async Task<BackgroundRenderResult?> LoadCachedAsync(RandomBackgroundType type)
    {
        var path = GetCacheFilePath(type);
        if (!File.Exists(path)) return null;

        try
        {
            var image = await BackgroundImageLoader.LoadFileAsync(path);
            return new BackgroundRenderResult { ImageSource = image, IsVideo = false };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RandomBackground] 缓存加载失败: {ex.Message}");
            TryDelete(path);
            return null;
        }
    }

    public static async Task<BackgroundRenderResult?> FetchAndCacheAsync(RandomBackgroundType type)
    {
        var generation = Volatile.Read(ref _generation);
        var firstTask = DownloadAsync(type);
        var secondTask = DownloadAsync(type);
        await Task.WhenAll(firstTask, secondTask);
        var first = await firstTask;
        var second = await secondTask;

        if (generation != Volatile.Read(ref _generation))
            return null;

        var display = await CreateResultAsync(first);
        byte[]? cache;
        if (display != null)
        {
            cache = second ?? first;
        }
        else
        {
            display = await CreateResultAsync(second);
            cache = second;
        }

        if (display == null || cache == null || cache.Length == 0)
            return null;

        await SaveNextCacheAsync(cache, type, generation);
        return display;
    }

    private static async Task<byte[]?> DownloadAsync(RandomBackgroundType type)
    {
        try
        {
            using var response = await Client.GetAsync(GetEndpoint(type), HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(mediaType) ||
                !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                return null;

            if (response.Content.Headers.ContentLength is > MaxDownloadBytes)
                return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            return bytes.Length is > 0 and <= MaxDownloadBytes ? bytes : null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RandomBackground] 图片下载失败: {ex.Message}");
            return null;
        }
    }

    private static async Task<BackgroundRenderResult?> CreateResultAsync(byte[]? bytes)
    {
        if (bytes is not { Length: > 0 })
            return null;

        var temp = Path.Combine(CacheDirectory, $"display_{Guid.NewGuid():N}.img");
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            await File.WriteAllBytesAsync(temp, bytes);
            var image = await BackgroundImageLoader.LoadFileAsync(temp);
            return new BackgroundRenderResult { ImageSource = image, IsVideo = false };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RandomBackground] 图片解码失败: {ex.Message}");
            return null;
        }
        finally
        {
            TryDelete(temp);
        }
    }

    private static async Task SaveNextCacheAsync(byte[] bytes, RandomBackgroundType type, int generation)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            var temp = Path.Combine(CacheDirectory, $"next_{Guid.NewGuid():N}.tmp");
            await File.WriteAllBytesAsync(temp, bytes);

            if (generation != Volatile.Read(ref _generation))
            {
                TryDelete(temp);
                return;
            }

            var cachePath = GetCacheFilePath(type);
            File.Move(temp, cachePath, true);

            if (generation != Volatile.Read(ref _generation))
                TryDelete(cachePath);
        }
        catch (Exception ex)
        {
            if (generation == Volatile.Read(ref _generation))
                Debug.WriteLine($"[RandomBackground] 缓存写入失败: {ex.Message}");
        }
    }

    public static void ClearCache()
    {
        Interlocked.Increment(ref _generation);
        try
        {
            if (Directory.Exists(CacheDirectory))
                Directory.Delete(CacheDirectory, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RandomBackground] 缓存清理失败: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
        }
    }
}
