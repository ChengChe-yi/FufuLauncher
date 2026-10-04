/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FufuLauncher.Helpers;
using FufuLauncher.Models.Download;

namespace FufuLauncher.Services.Download;

/// <summary>
/// 并行分段下载服务。
///
/// <para><b>调度策略</b>：把文件切成 <c>分段数 × 倍数</c> 个小段放入队列，
/// 各工作线程竞争领取（工作窃取），快连接自动多干，无需中央调度。</para>
///
/// <para><b>限速</b>：支持全局速率与单连接速率两级，均为令牌桶实现；速率为 0 时该项不生效。</para>
///
/// <para><b>超时</b>：<see cref="HttpClient.Timeout"/> 设为无限，改由「停滞看门狗」判定
/// （连续 <see cref="ParallelDownloadOptions.StallTimeout"/> 无数据即中断重试），
/// 因此大文件不会被固定的整体超时误杀；另可用
/// <see cref="ParallelDownloadOptions.OverallTimeout"/> 设置总时限。</para>
///
/// <para><b>可靠性</b>：分片级指数退避重试、可选断点续传、可选哈希/大小校验，校验通过后原子改名落盘。</para>
/// </summary>
public sealed class ParallelDownloadService
{
    private const string DefaultUserAgent = "FufuLauncher/1.0";

    /// <summary>续传状态文件的后缀。</summary>
    private const string ResumeStateSuffix = ".state.json";

    /// <summary>停滞看门狗的检查周期。</summary>
    private static readonly TimeSpan StallCheckInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// 下载单个文件。
    /// </summary>
    /// <param name="url">源 URL。</param>
    /// <param name="destinationPath">目标路径（含文件名）。</param>
    /// <param name="options">下载参数；null 使用 <see cref="ParallelDownloadOptions.Default"/>。</param>
    /// <param name="progress">进度回调；可为 null。</param>
    /// <param name="token">取消令牌。</param>
    /// <returns>最终文件路径，等同 <paramref name="destinationPath"/>。</returns>
    public async Task<string> DownloadAsync(
        string url,
        string destinationPath,
        ParallelDownloadOptions? options = null,
        IProgress<ParallelDownloadProgress>? progress = null,
        CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var opt = (options ?? ParallelDownloadOptions.Default).Validate();

        string fullPath = Path.GetFullPath(destinationPath);
        string directory = Path.GetDirectoryName(fullPath)
                           ?? throw new ArgumentException("目标路径缺少目录部分。", nameof(destinationPath));
        Directory.CreateDirectory(directory);

        if (File.Exists(fullPath) && !opt.OverwriteExisting)
        {
            throw new DownloadFailedException(
                $"目标文件已存在: {fullPath}", url, 0, 0, DownloadFailureReason.TargetUnwritable);
        }

        string workPath = opt.UseTemporaryFile ? fullPath + ".part" : fullPath;
        string resumeStatePath = workPath + ResumeStateSuffix;

        // 总时长上限：由 Ct 触发，再据 token 是否被取消区分「超时」与「调用方取消」
        using var overallCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (opt.OverallTimeout is { } overall && overall > TimeSpan.Zero)
        {
            overallCts.CancelAfter(overall);
        }

        CancellationToken ct = overallCts.Token;
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = opt.MaxConnectionsPerServer,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                ConnectTimeout = opt.ConnectTimeout,
                EnableMultipleHttp2Connections = true,
            };
            // Timeout=Infinite：大文件下载不应被固定整体超时打断，
            // 改由停滞看门狗 + OverallTimeout 控制。
            using var client = new HttpClient(handler, disposeHandler: true)
            {
                Timeout = Timeout.InfiniteTimeSpan,
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd(opt.UserAgent ?? DefaultUserAgent);
            // 禁用透明压缩，确保 Range 偏移与实际写入字节一致
            client.DefaultRequestHeaders.AcceptEncoding.Clear();
            client.DefaultRequestHeaders.AcceptEncoding.Add(
                new System.Net.Http.Headers.StringWithQualityHeaderValue("identity"));

            var probe = await ProbeAsync(client, url, opt, ct).ConfigureAwait(false);
            long totalSize = opt.ExpectedSize ?? probe.ContentLength;

            if (opt.ExpectedSize is { } expected && probe.ContentLength > 0 && probe.ContentLength != expected)
            {
                throw new DownloadFailedException(
                    $"文件大小与预期不符: 服务端 {probe.ContentLength}B，预期 {expected}B",
                    url, probe.ContentLength, 0, DownloadFailureReason.IntegrityMismatch);
            }

            bool canParallel = probe.SupportsRange
                               && totalSize > opt.MinParallelFileSize
                               && opt.MaxParallelSegments > 1;

            if (!canParallel && !probe.SupportsRange && !opt.AllowRangeFallback)
            {
                throw new DownloadFailedException(
                    "服务端不支持分段下载（缺少 Accept-Ranges: bytes）。",
                    url, totalSize, 0, DownloadFailureReason.RangeNotSupported);
            }

            if (canParallel)
            {
                try
                {
                    await DownloadParallelAsync(client, url, workPath, totalSize, opt, progress, ct, resumeStatePath)
                        .ConfigureAwait(false);
                }
                catch (DownloadFailedException ex)
                    when (ex.Reason == DownloadFailureReason.RangeNotSupported && opt.AllowRangeFallback)
                {
                    // 服务端声称支持 Range 但实际返回完整内容 → 降级单线程重下
                    Debug.WriteLine("[ParallelDownload] 服务端忽略 Range，降级为单线程");
                    await DownloadSingleThreadAsync(client, url, workPath, totalSize, opt, progress, ct)
                        .ConfigureAwait(false);
                }
            }
            else
            {
                Debug.WriteLine(
                    $"[ParallelDownload] 单线程（支持分段={probe.SupportsRange}, 大小={totalSize}B）");
                await DownloadSingleThreadAsync(client, url, workPath, totalSize, opt, progress, ct)
                    .ConfigureAwait(false);
            }

            long actualLength = new FileInfo(workPath).Length;
            if (totalSize > 0 && actualLength != totalSize)
            {
                throw new DownloadFailedException(
                    $"下载大小不符: 实际 {actualLength}B，预期 {totalSize}B",
                    url, totalSize, 0, DownloadFailureReason.IntegrityMismatch);
            }

            await VerifyHashAsync(workPath, url, opt, totalSize, ct).ConfigureAwait(false);

            if (opt.UseTemporaryFile)
            {
                File.Move(workPath, fullPath, overwrite: true);
            }

            TryDelete(resumeStatePath);

            stopwatch.Stop();
            Debug.WriteLine(
                $"[ParallelDownload] 完成 {Path.GetFileName(fullPath)} " +
                $"({actualLength / 1024.0 / 1024.0:F2} MB, {stopwatch.Elapsed.TotalSeconds:F1}s)");

            progress?.Report(new ParallelDownloadProgress
            {
                BytesDownloaded = actualLength,
                TotalBytes = totalSize > 0 ? totalSize : actualLength,
                BytesPerSecond = 0,
                CompletedChunks = 1,
                TotalChunks = 1,
                IsCompleted = true,
            });

            return fullPath;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            Cleanup(workPath, resumeStatePath, opt);
            throw new DownloadFailedException(
                "下载超时。", url, 0, 0, DownloadFailureReason.TimedOut);
        }
        catch (OperationCanceledException)
        {
            Cleanup(workPath, resumeStatePath, opt);
            throw new DownloadFailedException(
                "下载已取消。", url, 0, 0, DownloadFailureReason.Cancelled);
        }
        catch (DownloadFailedException)
        {
            Cleanup(workPath, resumeStatePath, opt);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            Cleanup(workPath, resumeStatePath, opt);
            throw new DownloadFailedException(
                $"目标路径不可写: {ex.Message}", url, 0, 0,
                DownloadFailureReason.TargetUnwritable, ex);
        }
        catch (Exception ex)
        {
            Cleanup(workPath, resumeStatePath, opt);
            throw new DownloadFailedException(
                $"下载失败: {ex.Message}", url, 0, 0, DownloadFailureReason.Unknown, ex);
        }
    }

    /// <summary>
    /// 批量下载，限制同时进行的文件数。单个文件失败不影响其余项，
    /// 失败详情在结果的 <see cref="ParallelDownloadResult.Error"/> 中返回。
    /// </summary>
    public async Task<IReadOnlyList<ParallelDownloadResult>> DownloadManyAsync(
        IEnumerable<ParallelDownloadItem> items,
        ParallelDownloadOptions? options = null,
        IProgress<ParallelDownloadProgress>? progress = null,
        int maxConcurrentFiles = 3,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        var list = items.ToList();
        var results = new ParallelDownloadResult[list.Count];
        using var gate = new SemaphoreSlim(Math.Max(maxConcurrentFiles, 1));

        // 不把 token 传给 Task.Run：否则取消时任务可能不启动，results 留空。
        // 取消由 DownloadAsync 内部处理并落成失败结果。
        var tasks = list.Select((item, index) => Task.Run(async () =>
        {
            try
            {
                await gate.WaitAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                results[index] = new ParallelDownloadResult(item.Url, null, ex);
                return;
            }

            try
            {
                string path = await DownloadAsync(
                    item.Url, item.DestinationPath, item.Options ?? options, progress, token)
                    .ConfigureAwait(false);
                results[index] = new ParallelDownloadResult(item.Url, path, null);
            }
            catch (Exception ex)
            {
                results[index] = new ParallelDownloadResult(item.Url, null, ex);
            }
            finally
            {
                gate.Release();
            }
        })).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return results;
    }

    // ================================================================ 预检

    private readonly record struct ProbeResult(bool SupportsRange, long ContentLength);

    /// <summary>
    /// HEAD 预检：取文件大小与 Accept-Ranges。
    /// <para>部分服务端不支持 HEAD（405/501），此时退回 <c>Range: bytes=0-0</c> 的 GET 探测。</para>
    /// </summary>
    private static async Task<ProbeResult> ProbeAsync(
        HttpClient client, string url, ParallelDownloadOptions opt, CancellationToken ct)
    {
        using (var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            probeCts.CancelAfter(opt.RequestTimeout);

            HttpResponseMessage? headResp = null;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                headResp = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, probeCts.Token)
                    .ConfigureAwait(false);

                if (headResp.IsSuccessStatusCode)
                {
                    return new ProbeResult(SupportsRange(headResp), headResp.Content.Headers.ContentLength ?? 0);
                }

                if (headResp.StatusCode is not (HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented))
                {
                    throw new DownloadFailedException(
                        $"HEAD 预检失败: HTTP {(int)headResp.StatusCode} {headResp.ReasonPhrase}",
                        url, 0, 0, DownloadFailureReason.PreFlightFailed);
                }
            }
            catch (HttpRequestException ex)
            {
                throw new DownloadFailedException(
                    $"HEAD 预检网络错误: {ex.Message}", url, 0, 0,
                    DownloadFailureReason.PreFlightFailed, ex);
            }
            finally
            {
                headResp?.Dispose();
            }

            // HEAD 不被支持 → 用 1 字节的 Range GET 探测
            using var probeReq = new HttpRequestMessage(HttpMethod.Get, url);
            probeReq.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 0);
            using var probeResp = await client.SendAsync(probeReq, HttpCompletionOption.ResponseHeadersRead, probeCts.Token)
                .ConfigureAwait(false);

            if (!probeResp.IsSuccessStatusCode)
            {
                throw new DownloadFailedException(
                    $"预检失败: HTTP {(int)probeResp.StatusCode} {probeResp.ReasonPhrase}",
                    url, 0, 0, DownloadFailureReason.PreFlightFailed);
            }

            long length = probeResp.Content.Headers.ContentRange?.Length
                          ?? probeResp.Content.Headers.ContentLength
                          ?? 0;

            return new ProbeResult(SupportsRange(probeResp), length);
        }
    }

    private static bool SupportsRange(HttpResponseMessage resp) =>
        resp.Headers.AcceptRanges?.Any(r => r.Equals("bytes", StringComparison.OrdinalIgnoreCase)) == true;

    // ================================================================ 单线程

    /// <summary>单线程流式下载：用于不支持分段，或文件小到不值得多连接（&lt; 1MB）的场景。</summary>
    private static async Task DownloadSingleThreadAsync(
        HttpClient client, string url, string workPath,
        long totalSize, ParallelDownloadOptions opt,
        IProgress<ParallelDownloadProgress>? progress, CancellationToken ct)
    {
        // HttpClient.Timeout 已设为无限，故此处必须自己看住停滞，
        // 否则卡死的连接会永久阻塞。
        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        long lastProgressTicks = Environment.TickCount64;

        PeriodicTimer? stallTimer = null;
        Task? watchdog = null;
        if (opt.StallTimeout > TimeSpan.Zero)
        {
            stallTimer = new PeriodicTimer(StallCheckInterval);
            watchdog = RunStallWatchdogAsync(
                stallTimer, attemptCts, () => Volatile.Read(ref lastProgressTicks), opt.StallTimeout);
        }

        try
        {
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            await using var source = await resp.Content.ReadAsStreamAsync(attemptCts.Token).ConfigureAwait(false);
            await using var destination = new FileStream(
                workPath, FileMode.Create, FileAccess.Write, FileShare.None, opt.BufferSize, useAsync: true);

            var limiter = new TokenBucketRateLimiter(opt.MaxBytesPerSecond);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(opt.BufferSize);
            long downloaded = 0;
            long lastReportTicks = Environment.TickCount64;
            long lastBytes = 0;

            try
            {
                while (true)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(0, opt.BufferSize), attemptCts.Token)
                        .ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await limiter.AcquireAsync(read, attemptCts.Token).ConfigureAwait(false);
                    await destination.WriteAsync(buffer.AsMemory(0, read), attemptCts.Token).ConfigureAwait(false);

                    downloaded += read;
                    Volatile.Write(ref lastProgressTicks, Environment.TickCount64);

                    long now = Environment.TickCount64;
                    double elapsed = (now - lastReportTicks) / 1000.0;
                    if (elapsed >= opt.ProgressReportInterval.TotalSeconds)
                    {
                        progress?.Report(new ParallelDownloadProgress
                        {
                            BytesDownloaded = downloaded,
                            TotalBytes = totalSize,
                            BytesPerSecond = (downloaded - lastBytes) / elapsed,
                            TotalChunks = 1,
                            ActiveSegments = 1,
                        });
                        lastBytes = downloaded;
                        lastReportTicks = now;
                    }
                }

                await destination.FlushAsync(attemptCts.Token).ConfigureAwait(false);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new DownloadFailedException(
                "单线程下载停滞超时。", url, totalSize, 0, DownloadFailureReason.Stalled);
        }
        finally
        {
            stallTimer?.Dispose();
            if (watchdog != null)
            {
                try { await watchdog.ConfigureAwait(false); }
                catch { /* 看门狗自身异常一律忽略 */ }
            }
        }
    }

    // ================================================================ 并行

    /// <summary>并行阶段的共享可变状态。</summary>
    private sealed class ParallelState
    {
        public long TotalDownloaded;
        public int CompletedChunks;
        public int ActiveSegments;
        public int FailedChunks;
        public int TotalChunks;
        public Exception? LastError;

        public long LastReportTicks = Environment.TickCount64;
        public long LastReportBytes;
        public readonly object ReportLock = new();
        public readonly object ErrorLock = new();
    }

    private async Task DownloadParallelAsync(
        HttpClient client, string url, string workPath, long totalSize,
        ParallelDownloadOptions opt, IProgress<ParallelDownloadProgress>? progress,
        CancellationToken ct, string resumeStatePath)
    {
        var chunks = BuildChunks(totalSize, opt);
        Debug.WriteLine(
            $"[ParallelDownload] 并行 {chunks.Count} 小段 × {opt.MaxParallelSegments} 线程 " +
            $"(总计 {totalSize / 1024.0 / 1024.0:F2}MB)");

        // 断点续传：读回已完成段（参数变化时自动作废）
        var completed = opt.EnableResume
            ? LoadResumeState(resumeStatePath, totalSize, chunks.Count)
            : new HashSet<int>();

        long alreadyDone = 0;
        foreach (int i in completed)
        {
            alreadyDone += chunks[i].End - chunks[i].Start + 1;
        }

        // 预分配：SetLength 一次扩展，避免并发分片写入造成碎片化
        using var handle = File.OpenHandle(
            workPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite,
            FileOptions.Asynchronous);

        if (RandomAccess.GetLength(handle) != totalSize)
        {
            RandomAccess.SetLength(handle, totalSize);
        }

        var state = new ParallelState
        {
            TotalDownloaded = alreadyDone,
            CompletedChunks = completed.Count,
            TotalChunks = chunks.Count,
        };

        var queue = new ConcurrentQueue<int>();
        for (int i = 0; i < chunks.Count; i++)
        {
            if (!completed.Contains(i))
            {
                queue.Enqueue(i);
            }
        }

        var globalLimiter = new TokenBucketRateLimiter(opt.MaxBytesPerSecond);
        var resumeLock = new object();

        // 某个分片发现服务端忽略 Range 时，用它中止其余在途分片，让并行阶段尽快退出。
        using var parallelAbort = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var workers = new List<Task>(opt.MaxParallelSegments);
        for (int w = 0; w < opt.MaxParallelSegments; w++)
        {
            // 单连接限速：每个工作线程各持一个令牌桶
            var segmentLimiter = new TokenBucketRateLimiter(opt.MaxBytesPerSecondPerSegment);
            workers.Add(Task.Run(async () =>
            {
                Interlocked.Increment(ref state.ActiveSegments);
                try
                {
                    while (queue.TryDequeue(out int index))
                    {
                        parallelAbort.Token.ThrowIfCancellationRequested();
                        var (start, end) = chunks[index];
                        try
                        {
                            await DownloadChunkAsync(
                                client, url, start, end, handle, parallelAbort.Token,
                                state, totalSize, opt, progress, globalLimiter, segmentLimiter)
                                .ConfigureAwait(false);

                            Interlocked.Increment(ref state.CompletedChunks);
                            if (opt.EnableResume)
                            {
                                lock (resumeLock)
                                {
                                    completed.Add(index);
                                    SaveResumeState(resumeStatePath, totalSize, chunks.Count, completed);
                                }
                            }
                        }
                        catch (DownloadFailedException ex)
                            when (ex.Reason == DownloadFailureReason.RangeNotSupported)
                        {
                            // 必须原样上抛：外层依赖它决定是否降级为单线程，
                            // 不能混进 “ChunksFailed” 里被吞掉。
                            lock (state.ErrorLock)
                            {
                                state.LastError = ex;
                            }

                            Interlocked.Increment(ref state.FailedChunks);
                            parallelAbort.Cancel();
                            throw;
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref state.FailedChunks);
                            lock (state.ErrorLock)
                            {
                                state.LastError = ex;
                            }
                        }
                    }
                }
                finally
                {
                    Interlocked.Decrement(ref state.ActiveSegments);
                }
            }, CancellationToken.None));
        }

        try
        {
            await Task.WhenAll(workers).ConfigureAwait(false);
        }
        catch (DownloadFailedException ex) when (ex.Reason == DownloadFailureReason.RangeNotSupported)
        {
            throw;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            // 仅因 RangeNotSupported 触发的内部中止：改抛真正的成因
            Exception? cause;
            lock (state.ErrorLock)
            {
                cause = state.LastError;
            }

            if (cause is DownloadFailedException { Reason: DownloadFailureReason.RangeNotSupported } rns)
            {
                throw rns;
            }

            throw;
        }

        if (Volatile.Read(ref state.FailedChunks) > 0)
        {
            ct.ThrowIfCancellationRequested();

            Exception? last;
            lock (state.ErrorLock)
            {
                last = state.LastError;
            }

            int failed = Volatile.Read(ref state.FailedChunks);
            throw new DownloadFailedException(
                $"下载失败: {failed} 个小段出错" +
                (last != null ? $"（{last.GetType().Name}: {last.Message}）" : ""),
                url, totalSize, failed, DownloadFailureReason.ChunksFailed, last)
            {
                BytesDownloaded = Interlocked.Read(ref state.TotalDownloaded),
            };
        }

        if (opt.EnableResume)
        {
            TryDelete(resumeStatePath);
        }
    }

    /// <summary>把文件切成小段（末段吃掉余数）。</summary>
    private static List<(long Start, long End)> BuildChunks(long totalSize, ParallelDownloadOptions opt)
    {
        int totalChunks = Math.Max(opt.MaxParallelSegments * opt.ChunkMultiplier, 1);
        long chunkSize = totalSize / totalChunks;

        // 段太小会产生过多请求 → 按 MinChunkSize 收敛
        if (chunkSize < opt.MinChunkSize)
        {
            totalChunks = (int)Math.Max(totalSize / opt.MinChunkSize, 1);
            chunkSize = totalSize / totalChunks;
        }

        totalChunks = Math.Max(totalChunks, 1);
        var chunks = new List<(long, long)>(totalChunks);
        for (int i = 0; i < totalChunks; i++)
        {
            long start = i * chunkSize;
            long end = i == totalChunks - 1 ? totalSize - 1 : start + chunkSize - 1;
            chunks.Add((start, end));
        }

        return chunks;
    }

    /// <summary>
    /// 下载单个小段：停滞看门狗 + 指数退避重试。
    /// <para>重试时从段首重新写入，覆盖上一次留下的残缺数据。</para>
    /// </summary>
    private static async Task DownloadChunkAsync(
        HttpClient client, string url, long start, long end,
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        CancellationToken ct, ParallelState state, long totalSize,
        ParallelDownloadOptions opt, IProgress<ParallelDownloadProgress>? progress,
        TokenBucketRateLimiter globalLimiter, TokenBucketRateLimiter segmentLimiter)
    {
        long chunkSize = end - start + 1;
        Exception? lastError = null;

        for (int attempt = 0; attempt <= opt.MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            long lastProgressTicks = Environment.TickCount64;

            PeriodicTimer? stallTimer = null;
            Task? watchdog = null;
            if (opt.StallTimeout > TimeSpan.Zero)
            {
                stallTimer = new PeriodicTimer(StallCheckInterval);
                watchdog = RunStallWatchdogAsync(stallTimer, attemptCts, () => Volatile.Read(ref lastProgressTicks), opt.StallTimeout);
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(opt.BufferSize);
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

                using var resp = await client.SendAsync(
                    req, HttpCompletionOption.ResponseHeadersRead, attemptCts.Token).ConfigureAwait(false);

                // 服务端忽略 Range 返回 200（完整内容）→ 无法用于分段写入
                if (resp.StatusCode == HttpStatusCode.OK)
                {
                    throw new DownloadFailedException(
                        "服务端忽略了 Range 请求，返回完整内容。",
                        url, totalSize, 1, DownloadFailureReason.RangeNotSupported);
                }

                resp.EnsureSuccessStatusCode();

                await using var stream = await resp.Content.ReadAsStreamAsync(attemptCts.Token)
                    .ConfigureAwait(false);

                long writePos = start;
                long written = 0;
                while (true)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(0, opt.BufferSize), attemptCts.Token)
                        .ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    await globalLimiter.AcquireAsync(read, attemptCts.Token).ConfigureAwait(false);
                    await segmentLimiter.AcquireAsync(read, attemptCts.Token).ConfigureAwait(false);

                    await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), writePos, attemptCts.Token)
                        .ConfigureAwait(false);

                    writePos += read;
                    written += read;
                    Volatile.Write(ref lastProgressTicks, Environment.TickCount64);
                    Interlocked.Add(ref state.TotalDownloaded, read);

                    ReportProgress(state, totalSize, opt, progress);
                }

                if (written < chunkSize)
                {
                    throw new IOException($"小段不完整: 预期 {chunkSize}B，实际 {written}B");
                }

                return;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // 停滞看门狗触发 → 可重试
                lastError = new DownloadFailedException(
                    "小段下载停滞超时。", url, totalSize, 1, DownloadFailureReason.Stalled);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DownloadFailedException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                stallTimer?.Dispose();
                if (watchdog != null)
                {
                    try { await watchdog.ConfigureAwait(false); }
                    catch { /* 看门狗自身异常一律忽略 */ }
                }
            }

            if (attempt < opt.MaxRetries)
            {
                double backoff = opt.RetryBaseDelay.TotalMilliseconds * Math.Pow(2, attempt);
                var delay = TimeSpan.FromMilliseconds(
                    Math.Min(backoff, opt.RetryMaxDelay.TotalMilliseconds));
                Debug.WriteLine(
                    $"[ParallelDownload] 小段 [{start}-{end}] 第 {attempt + 1} 次失败，" +
                    $"{delay.TotalSeconds:F1}s 后重试" +
                    (lastError != null ? $"（{lastError.Message}）" : ""));
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
        }

        throw new IOException(
            $"小段 [{start}-{end}] 重试 {opt.MaxRetries} 次后仍失败", lastError);
    }

    /// <summary>停滞看门狗：超过阈值无数据则取消本次尝试。</summary>
    private static async Task RunStallWatchdogAsync(
        PeriodicTimer timer, CancellationTokenSource attemptCts,
        Func<long> lastProgressTicks, TimeSpan stallTimeout)
    {
        long thresholdMs = (long)stallTimeout.TotalMilliseconds;
        try
        {
            while (await timer.WaitForNextTickAsync(CancellationToken.None).ConfigureAwait(false))
            {
                if (Environment.TickCount64 - lastProgressTicks() > thresholdMs)
                {
                    attemptCts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    /// <summary>按配置的间隔节流上报进度。</summary>
    private static void ReportProgress(
        ParallelState state, long totalSize,
        ParallelDownloadOptions opt, IProgress<ParallelDownloadProgress>? progress)
    {
        if (progress == null)
        {
            return;
        }

        long now = Environment.TickCount64;
        bool report = false;
        double speed = 0;
        long downloaded = 0;

        lock (state.ReportLock)
        {
            double elapsed = (now - state.LastReportTicks) / 1000.0;
            if (elapsed >= opt.ProgressReportInterval.TotalSeconds)
            {
                downloaded = Interlocked.Read(ref state.TotalDownloaded);
                speed = (downloaded - state.LastReportBytes) / elapsed;
                state.LastReportBytes = downloaded;
                state.LastReportTicks = now;
                report = true;
            }
        }

        if (report)
        {
            progress.Report(new ParallelDownloadProgress
            {
                BytesDownloaded = downloaded,
                TotalBytes = totalSize,
                BytesPerSecond = speed,
                CompletedChunks = Volatile.Read(ref state.CompletedChunks),
                TotalChunks = state.TotalChunks,
                ActiveSegments = Volatile.Read(ref state.ActiveSegments),
            });
        }
    }

    // ================================================================ 校验

    private static async Task VerifyHashAsync(
        string path, string url, ParallelDownloadOptions opt, long totalSize, CancellationToken ct)
    {
        if (opt.HashAlgorithm == DownloadHashAlgorithm.None || string.IsNullOrWhiteSpace(opt.ExpectedHash))
        {
            return;
        }

        string actual;
        if (opt.HashAlgorithm == DownloadHashAlgorithm.Md5)
        {
            actual = await HashUtility.Md5FileAsync(path, ct).ConfigureAwait(false);
        }
        else
        {
            await using var stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 81920, useAsync: true);
            actual = await HashUtility.XxHash64HexAsync(stream, ct).ConfigureAwait(false);
        }

        if (!actual.Equals(opt.ExpectedHash, StringComparison.OrdinalIgnoreCase))
        {
            throw new DownloadFailedException(
                $"哈希校验失败: 实际 {actual}，预期 {opt.ExpectedHash}",
                url, totalSize, 0, DownloadFailureReason.IntegrityMismatch);
        }
    }

    // ================================================================ 断点续传

    /// <summary>续传状态文件结构。</summary>
    private sealed record ResumeState(long TotalSize, int TotalChunks, List<int> Completed);

    private static HashSet<int> LoadResumeState(string statePath, long totalSize, int totalChunks)
    {
        try
        {
            if (!File.Exists(statePath))
            {
                return new HashSet<int>();
            }

            var state = JsonSerializer.Deserialize<ResumeState>(File.ReadAllText(statePath));
            // 大小或分段数变了（例如改了并发设置）→ 旧记录不可信
            if (state == null || state.TotalSize != totalSize || state.TotalChunks != totalChunks)
            {
                Debug.WriteLine("[ParallelDownload] 续传记录与当前参数不符，忽略");
                return new HashSet<int>();
            }

            return new HashSet<int>(state.Completed.Where(i => i >= 0 && i < totalChunks));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ParallelDownload] 读取续传记录失败，忽略: {ex.Message}");
            return new HashSet<int>();
        }
    }

    private static void SaveResumeState(
        string statePath, long totalSize, int totalChunks, HashSet<int> completed)
    {
        try
        {
            var state = new ResumeState(totalSize, totalChunks, completed.OrderBy(i => i).ToList());
            File.WriteAllText(statePath, JsonSerializer.Serialize(state));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ParallelDownload] 写入续传记录失败: {ex.Message}");
        }
    }

    // ================================================================ 清理

    private static void Cleanup(string workPath, string resumeStatePath, ParallelDownloadOptions opt)
    {
        if (!opt.DeleteOnFailure)
        {
            return;
        }

        TryDelete(workPath);
        TryDelete(resumeStatePath);
    }

    private static void TryDelete(string path)
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
            Debug.WriteLine($"[ParallelDownload] 删除失败 {path}: {ex.Message}");
        }
    }
}

/// <summary>批量下载的单个条目。</summary>
/// <param name="Url">源 URL。</param>
/// <param name="DestinationPath">目标路径。</param>
/// <param name="Options">该项专用参数；null 则用批次的参数。</param>
public sealed record ParallelDownloadItem(
    string Url,
    string DestinationPath,
    ParallelDownloadOptions? Options = null);

/// <summary>批量下载中单个文件的结果。</summary>
/// <param name="Url">源 URL。</param>
/// <param name="Path">成功时的落盘路径；失败为 null。</param>
/// <param name="Error">失败时的异常；成功为 null。</param>
public sealed record ParallelDownloadResult(string Url, string? Path, Exception? Error)
{
    /// <summary>是否成功。</summary>
    public bool IsSuccess => Error == null && Path != null;
}
