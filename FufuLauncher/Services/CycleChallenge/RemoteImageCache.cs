/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Hashing;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services.CycleChallenge;

/// <summary>
/// 周期挑战远程图片磁盘缓存。
/// <para>
/// 语义对标 Starward 的 <c>Helpers/FileCache.cs</c>：
/// <list type="bullet">
/// <item>缓存目录：<c>%LocalAppData%/FufuLauncher/Data/cyclechallenge/images</c>（取 <see cref="AppPaths.DataDir"/>）。</item>
/// <item>文件名为 URL 的 XxHash64(8 字节) + MD5(16 字节) 共 24 字节转大写 HEX（48 字符）。</item>
/// <item>同一个 URL 只允许一个下载任务（单飞），其余调用方共享同一个 <see cref="Task{TResult}"/>。</item>
/// <item>下载先写 <c>*.tmp</c>，成功后 <see cref="File.Move(string, string, bool)"/> 原子替换。</item>
/// <item>支持 HTTP Range 断点续传；失败最多重试 3 次；最终失败返回 <c>null</c>，绝不向 UI 抛异常。</item>
/// </list>
/// </para>
/// <para>
/// 注意：单飞任务不属于某个具体调用方，调用方取消（例如控件 Unloaded）只会停止等待，
/// 不会中断共享下载 —— 这样其它控件仍能命中缓存；下载完成后会自行从单飞表移除。
/// </para>
/// </summary>
public static class RemoteImageCache
{
    /// <summary>缓存文件有效期：90 天。</summary>
    public static readonly TimeSpan CacheMaxAge = TimeSpan.FromDays(90);

    /// <summary>缓存目录中最多保留的图片数量，超出后按最后写入时间淘汰最旧的。</summary>
    public const int MaxCachedImageCount = 500;

    /// <summary>失败后的重试次数（不含首次请求）。</summary>
    private const int MaxRetryCount = 3;

    /// <summary>单次下载的整体超时时间（含连接、响应头与响应体）。</summary>
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(60);

    /// <summary>缓存键长度：XxHash64(8 字节) + MD5(16 字节) = 24 字节 → 48 个 HEX 字符。</summary>
    private const int CacheKeyByteCount = 24;

    /// <summary>单飞表：缓存键 → 延迟启动的下载任务（GetOrAdd 工厂可能执行多次，Lazy 保证只有存入的那一个真正开始下载）。</summary>
    private static readonly ConcurrentDictionary<string, Lazy<Task<string?>>> InFlight = new(StringComparer.Ordinal);

    /// <summary>全局共享 HttpClient（自动解压 + HTTP/2 多连接）。</summary>
    private static readonly HttpClient HttpClient = CreateHttpClient();

    /// <summary>图片缓存目录。</summary>
    public static string CacheFolder => Path.Combine(AppPaths.DataDir, "cyclechallenge", "images");

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            // 服务端常用 gzip/br 压缩图标，交给运行时自动解压
            AutomaticDecompression = DecompressionMethods.All,
            EnableMultipleHttp2Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            ConnectTimeout = TimeSpan.FromSeconds(15),
            // 图标使用 CDN，跟随重定向
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
        };

        return new HttpClient(handler)
        {
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher,
            // 整体超时由每次下载的 CancellationTokenSource 控制
            Timeout = System.Threading.Timeout.InfiniteTimeSpan,
        };
    }

    /// <summary>
    /// 获取远程图片的本地缓存路径。
    /// </summary>
    /// <param name="uri">远程图片地址，仅支持 http/https。</param>
    /// <param name="cancellationToken">调用方取消标记；取消只影响等待，不会中断共享下载。</param>
    /// <returns>本地文件绝对路径；任何失败（参数非法、网络错误、磁盘错误）均返回 <c>null</c>。</returns>
    public static async Task<string?> GetAsync(Uri? uri, CancellationToken cancellationToken = default)
    {
        try
        {
            if (uri is null || !uri.IsAbsoluteUri)
            {
                return null;
            }

            if (!IsHttpUri(uri))
            {
                return null;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return null;
            }

            string key = uri.ToString();
            Lazy<Task<string?>> lazy = InFlight.GetOrAdd(
                key,
                _ => new Lazy<Task<string?>>(
                    () => StartDownload(key, uri),
                    System.Threading.LazyThreadSafetyMode.ExecutionAndPublication));
            Task<string?> shared = lazy.Value;

            try
            {
                return await shared.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 调用方取消：只停止等待，返回 null
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemoteImageCache] 等待下载任务失败 {uri}: {ex.Message}");
                return null;
            }
            finally
            {
                RemoveEntry(key, shared);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteImageCache] GetAsync 异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 删除某个 URL 对应的缓存文件（例如图片文件损坏时由控件触发，下次重新下载）。
    /// </summary>
    public static void Invalidate(Uri? uri)
    {
        try
        {
            if (uri is null || !uri.IsAbsoluteUri)
            {
                return;
            }

            string path = Path.Combine(CacheFolder, GetCacheFileName(uri));
            DeleteFileIfExists(path);
            DeleteFileIfExists(path + ".tmp");

            // 只移除已完成的单飞任务；正在下载的任务让它跑完，避免同一 URL 并发写同一个 .tmp
            string key = uri.ToString();
            if (InFlight.TryGetValue(key, out Lazy<Task<string?>>? lazy)
                && lazy.IsValueCreated
                && lazy.Value.IsCompleted)
            {
                InFlight.TryRemove(key, out _);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteImageCache] Invalidate 异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 清理缓存：删除超过 TTL（90 天）的文件、残留的临时文件，并把图片总数限制在 500 张以内。
    /// 建议在页面后台加载完成后调用，不要在 UI 线程同步等待。
    /// </summary>
    public static async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() => CleanupCore(cancellationToken), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 忽略取消
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteImageCache] Cleanup 异常: {ex.Message}");
        }
    }

    #region 下载

    /// <summary>创建单飞任务，并在任务完成（含被遗弃的任务）后把自身从单飞表移除。</summary>
    private static Task<string?> StartDownload(string key, Uri uri)
    {
        Task<string?> task = DownloadWithRetryAsync(uri);
        _ = task.ContinueWith(
            completed => RemoveEntry(key, completed),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    /// <summary>从单飞表中移除指定任务（仅当表里还是同一个 Lazy 包装的同一个任务时才移除）。</summary>
    private static void RemoveEntry(string key, Task<string?> task)
    {
        try
        {
            if (InFlight.TryGetValue(key, out Lazy<Task<string?>>? lazy)
                && lazy.IsValueCreated
                && ReferenceEquals(lazy.Value, task))
            {
                InFlight.TryRemove(key, out _);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteImageCache] 移除单飞任务失败: {ex.Message}");
        }
    }

    private static async Task<string?> DownloadWithRetryAsync(Uri uri)
    {
        string folder = CacheFolder;
        string filePath = Path.Combine(folder, GetCacheFileName(uri));

        try
        {
            Directory.CreateDirectory(folder);

            if (IsUsableCacheFile(filePath))
            {
                return filePath;
            }

            // 过期或空文件先删除，避免 Range 续传接在坏文件后面
            DeleteFileIfExists(filePath);

            Exception? lastError = null;
            for (int attempt = 0; attempt <= MaxRetryCount; attempt++)
            {
                // 并发下载可能已经完成并落盘：重试前先复查，避免重复下载 / 写坏 .tmp
                if (IsUsableCacheFile(filePath))
                {
                    return filePath;
                }

                try
                {
                    await DownloadFileAsync(uri, filePath + ".tmp", filePath).ConfigureAwait(false);

                    if (IsUsableCacheFile(filePath))
                    {
                        return filePath;
                    }

                    throw new IOException("下载完成后缓存文件为空。");
                }
                catch (Exception ex) when (ex is HttpRequestException
                                               or IOException
                                               or TaskCanceledException
                                               or OperationCanceledException
                                               or UnauthorizedAccessException)
                {
                    lastError = ex;
                    Debug.WriteLine(
                        $"[RemoteImageCache] 下载失败 ({attempt + 1}/{MaxRetryCount + 1}) {uri}: {ex.Message}");

                    if (attempt < MaxRetryCount)
                    {
                        try
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(200 * (attempt + 1))).ConfigureAwait(false);
                        }
                        catch (Exception delayEx)
                        {
                            Debug.WriteLine($"[RemoteImageCache] 重试等待异常: {delayEx.Message}");
                            break;
                        }
                    }
                }
            }

            Debug.WriteLine($"[RemoteImageCache] 放弃下载 {uri}: {lastError?.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteImageCache] 下载流程异常 {uri}: {ex.Message}");
            return null;
        }
    }

    /// <summary>下载到临时文件，支持 Range 断点续传，成功后原子替换正式文件。</summary>
    private static async Task DownloadFileAsync(Uri uri, string tmpPath, string finalPath)
    {
        using var timeoutCts = new CancellationTokenSource(DownloadTimeout);
        CancellationToken token = timeoutCts.Token;

        using (FileStream fs = File.Open(tmpPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (fs.Length > 0)
            {
                // 上次下载中断，从已有长度继续
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(fs.Length, null);
            }

            using HttpResponseMessage response = await HttpClient
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentRange is { From: > 0 } range)
            {
                fs.Position = range.From.Value;
            }
            else
            {
                // 服务端不支持 Range（200）：丢弃旧内容从头写
                fs.Position = 0;
                fs.SetLength(0);
            }

            using Stream body = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            await body.CopyToAsync(fs, token).ConfigureAwait(false);
            await fs.FlushAsync(token).ConfigureAwait(false);
        }

        // 原子替换：Windows 上 File.Move(source, dest, overwrite: true) 为 MoveFileEx(REPLACE_EXISTING)
        File.Move(tmpPath, finalPath, true);
    }

    #endregion

    #region 缓存文件

    /// <summary>判断缓存文件是否可用（存在、非空、未超过 TTL）。</summary>
    private static bool IsUsableCacheFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                   && info.Length > 0
                   && DateTime.UtcNow - info.LastWriteTimeUtc <= CacheMaxAge;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteImageCache] 检查缓存文件失败 {path}: {ex.Message}");
            return false;
        }
    }

    /// <summary>缓存文件名：XxHash64(URL, 8 字节) + MD5(URL, 16 字节) 共 24 字节转 HEX。</summary>
    private static string GetCacheFileName(Uri uri)
    {
        string url = uri.ToString();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(url.Length));
        try
        {
            int length = Encoding.UTF8.GetBytes(url, buffer);
            ReadOnlySpan<byte> urlBytes = buffer.AsSpan(0, length);

            Span<byte> hashBytes = stackalloc byte[CacheKeyByteCount];
            XxHash64.Hash(urlBytes, hashBytes[..8]);
            MD5.HashData(urlBytes, hashBytes[8..]);
            return Convert.ToHexString(hashBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static void DeleteFileIfExists(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex)
        {
            // 文件可能正被 BitmapImage 占用，忽略即可
            Debug.WriteLine($"[RemoteImageCache] 删除缓存文件失败 {path}: {ex.Message}");
        }
    }

    private static void CleanupCore(CancellationToken cancellationToken)
    {
        string folder = CacheFolder;
        if (!Directory.Exists(folder))
        {
            return;
        }

        DateTime now = DateTime.UtcNow;
        DateTime staleTmpBefore = now - TimeSpan.FromDays(1);
        List<FileInfo> cachedFiles = new();

        foreach (string file in Directory.EnumerateFiles(folder))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                var info = new FileInfo(file);
                if (!info.Exists)
                {
                    continue;
                }

                if (string.Equals(info.Extension, ".tmp", StringComparison.OrdinalIgnoreCase))
                {
                    // 残留的临时文件，超过 1 天肯定是死文件
                    if (info.LastWriteTimeUtc < staleTmpBefore)
                    {
                        DeleteFileIfExists(file);
                    }

                    continue;
                }

                if (now - info.LastWriteTimeUtc > CacheMaxAge)
                {
                    DeleteFileIfExists(file);
                }
                else
                {
                    cachedFiles.Add(info);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RemoteImageCache] 清理文件失败 {file}: {ex.Message}");
            }
        }

        if (cachedFiles.Count <= MaxCachedImageCount)
        {
            return;
        }

        int removeCount = cachedFiles.Count - MaxCachedImageCount;
        foreach (FileInfo info in cachedFiles.OrderBy(static f => f.LastWriteTimeUtc).Take(removeCount))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            DeleteFileIfExists(info.FullName);
        }
    }

    private static bool IsHttpUri(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
