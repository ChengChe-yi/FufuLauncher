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

public sealed class ParallelDownloadService
{
    private const string DefaultUserAgent = "FufuLauncher/1.0";

    private const string ResumeStateSuffix = ".state.json";

    private static readonly TimeSpan StallCheckInterval = TimeSpan.FromSeconds(1);

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

        // 单文件下载：handler/client 生命周期覆盖本次调用。
        // 批处理请走 DownloadManyAsync，它在整批范围内复用同一个 client。
        using var handler = CreateHandler(opt);
        using var client = CreateClient(handler);
        return await DownloadCoreAsync(client, url, destinationPath, opt, progress, token)
            .ConfigureAwait(false);
    }

    private async Task<string> DownloadCoreAsync(
        HttpClient client,
        string url,
        string destinationPath,
        ParallelDownloadOptions opt,
        IProgress<ParallelDownloadProgress>? progress,
        CancellationToken token)
    {
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
                    // 服务端声称支持 Range 但实际返回完整内容 → 降级单线程重下。
                    // 单线程用 FileMode.Create 从头写同一个 workPath，
                    // 旧的分段续传记录就此失效，必须先删掉，否则下次会跳过实际未写的段。
                    Debug.WriteLine("[ParallelDownload] 服务端忽略 Range，降级为单线程");
                    TryDelete(resumeStatePath);
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
        if (list.Count == 0)
        {
            return results;
        }

        // 用批次内最大并发需求建一个 client，供全部文件共享。
        var batchOpt = (options ?? ParallelDownloadOptions.Default).Validate();
        int peakSegments = list
            .Select(i => (i.Options ?? batchOpt).Validate().MaxParallelSegments)
            .DefaultIfEmpty(batchOpt.MaxParallelSegments)
            .Max();

        // 调用方显式设过连接上限就保留（取批次与各项中的最大值），
        // 否则按批次并发推导。Validate() 会把 <=0 填成派生值，故只能看原始入参。
        int explicitConnections = new[] { options?.MaxConnectionsPerServer ?? 0 }
            .Concat(list.Select(i => i.Options?.MaxConnectionsPerServer ?? 0))
            .Max();

        var handlerOpt = batchOpt with
        {
            MaxConnectionsPerServer = explicitConnections > 0
                ? explicitConnections
                : Math.Max(peakSegments, maxConcurrentFiles),
        };

        using var handler = CreateHandler(handlerOpt);
        using var client = CreateClient(handler);
        using var gate = new SemaphoreSlim(Math.Max(maxConcurrentFiles, 1));

        // 不把 token 传给 Task.Run：否则取消时任务可能不启动，results 留空。
        // 取消由 DownloadCoreAsync 内部处理并落成失败结果。
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
                var itemOpt = (item.Options ?? batchOpt).Validate();
                string path = await DownloadCoreAsync(
                    client, item.Url, item.DestinationPath, itemOpt, progress, token)
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

    // ================================================================ 客户端

    private static SocketsHttpHandler CreateHandler(ParallelDownloadOptions opt) => new()
    {
        MaxConnectionsPerServer = opt.MaxConnectionsPerServer,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        ConnectTimeout = opt.ConnectTimeout,
        EnableMultipleHttp2Connections = true,
    };

    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler, disposeHandler: true)
    {
        // Timeout=Infinite：大文件不应被固定整体超时打断，
        // 改由停滞看门狗 + OverallTimeout 控制。
        Timeout = Timeout.InfiniteTimeSpan,
    };

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, ParallelDownloadOptions opt)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.UserAgent.ParseAdd(opt.UserAgent ?? DefaultUserAgent);
        // 禁用透明压缩，确保 Range 偏移与实际写入字节一致
        req.Headers.AcceptEncoding.Add(new System.Net.Http.Headers.StringWithQualityHeaderValue("identity"));
        return req;
    }

    // ================================================================ 预检

    private readonly record struct ProbeResult(bool SupportsRange, long ContentLength);

    private static async Task<ProbeResult> ProbeAsync(
        HttpClient client, string url, ParallelDownloadOptions opt, CancellationToken ct)
    {
        using (var probeCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
        {
            probeCts.CancelAfter(opt.RequestTimeout);

            HttpResponseMessage? headResp = null;
            try
            {
                using var req = CreateRequest(HttpMethod.Head, url, opt);
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
            using var probeReq = CreateRequest(HttpMethod.Get, url, opt);
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

    private static async Task DownloadSingleThreadAsync(
        HttpClient client, string url, string workPath,
        long totalSize, ParallelDownloadOptions opt,
        IProgress<ParallelDownloadProgress>? progress, CancellationToken ct)
    {
        // HttpClient.Timeout 已设为无限，故此处必须自己看住停滞，
        // 否则卡死的连接会永久阻塞。
        using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        long lastProgressTicks = Environment.TickCount64;

        // 看门狗在收到响应头之后才启动（建连/排队不计入停滞，理由同 DownloadChunkAsync）。
        PeriodicTimer? stallTimer = null;
        Task? watchdog = null;

        try
        {
            using var req = CreateRequest(HttpMethod.Get, url, opt);

            using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(attemptCts.Token);
            if (opt.RequestTimeout > TimeSpan.Zero)
            {
                sendCts.CancelAfter(opt.RequestTimeout);
            }

            HttpResponseMessage resp;
            try
            {
                resp = await client.SendAsync(
                    req, HttpCompletionOption.ResponseHeadersRead, sendCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                throw new DownloadFailedException(
                    $"建立连接或排队超时（>{opt.RequestTimeout.TotalSeconds:F0}s）。",
                    url, totalSize, 0, DownloadFailureReason.TimedOut);
            }

            using (resp)
            {
                resp.EnsureSuccessStatusCode();

                await using var source = await resp.Content.ReadAsStreamAsync(attemptCts.Token).ConfigureAwait(false);
                await using var destination = new FileStream(
                    workPath, FileMode.Create, FileAccess.Write, FileShare.None, opt.BufferSize, useAsync: true);

                if (opt.StallTimeout > TimeSpan.Zero)
                {
                    stallTimer = new PeriodicTimer(StallCheckInterval);
                    watchdog = RunStallWatchdogAsync(
                        stallTimer, attemptCts, () => Volatile.Read(ref lastProgressTicks), opt.StallTimeout);
                }

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

                        // 限速等待不算“停滞”（见 DownloadChunkAsync 的同款说明）。
                        Volatile.Write(ref lastProgressTicks, Environment.TickCount64);
                        await destination.WriteAsync(buffer.AsMemory(0, read), attemptCts.Token).ConfigureAwait(false);

                        downloaded += read;

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
            ? LoadResumeState(resumeStatePath, url, totalSize, chunks.Count)
            : new HashSet<int>();

        // 续传记录只说明“上次记到这里”，不代表 .part 里的数据还在。
        // 文件被删/被截断/被其它流程改写时，跳过的段会留下空洞，
        // 而 SetLength 会把空洞补零 —— 大小校验照样通过，产出静默损坏的文件。
        // 因此文件长度不符时一律丢弃续传记录，从头下载。
        if (completed.Count > 0 && !IsWorkFileIntact(workPath, totalSize))
        {
            Debug.WriteLine("[ParallelDownload] .part 文件缺失或长度不符，丢弃续传记录");
            completed.Clear();
            TryDelete(resumeStatePath);
        }

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
                                    SaveResumeState(resumeStatePath, url, totalSize, chunks.Count, completed);
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

    private static async Task DownloadChunkAsync(
        HttpClient client, string url, long start, long end,
        Microsoft.Win32.SafeHandles.SafeFileHandle handle,
        CancellationToken ct, ParallelState state, long totalSize,
        ParallelDownloadOptions opt, IProgress<ParallelDownloadProgress>? progress,
        TokenBucketRateLimiter globalLimiter, TokenBucketRateLimiter segmentLimiter)
    {
        long chunkSize = end - start + 1;
        Exception? lastError = null;

        // 本次尝试已写入的字节数；失败重试前需从总进度中扣回。
        long writtenThisAttempt = 0;

        for (int attempt = 0; attempt <= opt.MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            long lastProgressTicks = Environment.TickCount64;
            writtenThisAttempt = 0;

            // 看门狗在收到响应头之后才启动：在此之前请求可能只是排在连接池队列里
            // （同主机并发超过 MaxConnectionsPerServer 时），那不算传输停滞。
            PeriodicTimer? stallTimer = null;
            Task? watchdog = null;

            byte[] buffer = ArrayPool<byte>.Shared.Rent(opt.BufferSize);
            try
            {
                using var req = CreateRequest(HttpMethod.Get, url, opt);
                req.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

                // 建连与排队单独限时：超时按可重试失败处理，不计入停滞。
                using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(attemptCts.Token);
                if (opt.RequestTimeout > TimeSpan.Zero)
                {
                    sendCts.CancelAfter(opt.RequestTimeout);
                }

                HttpResponseMessage resp;
                try
                {
                    resp = await client.SendAsync(
                        req, HttpCompletionOption.ResponseHeadersRead, sendCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    throw new IOException($"建立连接或排队超时（>{opt.RequestTimeout.TotalSeconds:F0}s）。");
                }

                using (resp)
                {
                    // 服务端忽略 Range 返回 200（完整内容）→ 无法用于分段写入
                    if (resp.StatusCode == HttpStatusCode.OK)
                    {
                        throw new DownloadFailedException(
                            "服务端忽略了 Range 请求，返回完整内容。",
                            url, totalSize, 1, DownloadFailureReason.RangeNotSupported);
                    }

                    resp.EnsureSuccessStatusCode();

                    // 206 必须覆盖请求的那一段，否则内容会被写到错误偏移。
                    var contentRange = resp.Content.Headers.ContentRange;
                    if (contentRange?.From != start || contentRange.To != end)
                    {
                        throw new DownloadFailedException(
                            $"分段响应范围不符: 请求 [{start}-{end}]，返回 " +
                            (contentRange == null
                                ? "无 Content-Range 头"
                                : $"[{contentRange.From}-{contentRange.To}]"),
                            url, totalSize, 1, DownloadFailureReason.RangeNotSupported);
                    }

                    await using var stream = await resp.Content.ReadAsStreamAsync(attemptCts.Token)
                        .ConfigureAwait(false);

                    // 已有响应头，此后无数据才算停滞。
                    if (opt.StallTimeout > TimeSpan.Zero)
                    {
                        stallTimer = new PeriodicTimer(StallCheckInterval);
                        watchdog = RunStallWatchdogAsync(
                            stallTimer, attemptCts, () => Volatile.Read(ref lastProgressTicks), opt.StallTimeout);
                    }

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

                        // 限速等待不算“停滞”：令牌桶没有公平性保证，多线程共享时
                        // 单个线程的等待可达 N×BufferSize/速率，会超过 StallTimeout。
                        // 因此拿到配额后重新计时，再开始写。
                        Volatile.Write(ref lastProgressTicks, Environment.TickCount64);

                        await RandomAccess.WriteAsync(handle, buffer.AsMemory(0, read), writePos, attemptCts.Token)
                            .ConfigureAwait(false);

                        writePos += read;
                        written += read;
                        writtenThisAttempt += read;
                        Interlocked.Add(ref state.TotalDownloaded, read);
                        ReportProgress(state, totalSize, opt, progress);
                    }

                    if (written < chunkSize)
                    {
                        throw new IOException($"小段不完整: 预期 {chunkSize}B，实际 {written}B");
                    }

                    return;
                }
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

            // 能走到这里说明本次尝试失败了（成功路径在 try 内 return）。
            // 把本段已写入的字节扣回：重试会从段首重写，最终失败时这些字节也不算完成。
            // 不扣则 TotalDownloaded 虚高，百分比提前到 100、速度与 ETA 失真，
            // 异常里的 BytesDownloaded 也会误导续传判断。
            if (writtenThisAttempt > 0)
            {
                Interlocked.Add(ref state.TotalDownloaded, -writtenThisAttempt);
                writtenThisAttempt = 0;
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

    private static bool IsWorkFileIntact(string workPath, long totalSize)
    {
        try
        {
            var info = new FileInfo(workPath);
            return info.Exists && info.Length == totalSize;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ParallelDownload] 检查 .part 文件失败: {ex.Message}");
            return false;
        }
    }

    private sealed record ResumeState(long TotalSize, int TotalChunks, string UrlHash, List<int> Completed);

    // 记录必须绑定来源 URL：同一目标路径换了下载源时，大小与分段数可能完全一致，
    // 若沿用旧记录就会跳过旧源的段、其余从新源取，拼出内容混杂的文件。
    private static string ComputeUrlHash(string url) =>
        HashUtility.Md5Bytes(System.Text.Encoding.UTF8.GetBytes(url));

    private static HashSet<int> LoadResumeState(
        string statePath, string url, long totalSize, int totalChunks)
    {
        try
        {
            if (!File.Exists(statePath))
            {
                return new HashSet<int>();
            }

            var state = JsonSerializer.Deserialize<ResumeState>(File.ReadAllText(statePath));
            // 来源、大小或分段数任一变化（例如换了下载源、改了并发设置）→ 旧记录不可信
            if (state == null
                || state.TotalSize != totalSize
                || state.TotalChunks != totalChunks
                || !string.Equals(state.UrlHash, ComputeUrlHash(url), StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("[ParallelDownload] 续传记录与当前来源或参数不符，忽略");
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
        string statePath, string url, long totalSize, int totalChunks, HashSet<int> completed)
    {
        try
        {
            var state = new ResumeState(
                totalSize, totalChunks, ComputeUrlHash(url), completed.OrderBy(i => i).ToList());
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
        if (opt.EnableResume)
        {
            // 保留 .part 与 .state.json，供下次续传。
            return;
        }

        if (opt.DeleteOnFailure && opt.UseTemporaryFile)
        {
            TryDelete(workPath);
        }

        if (opt.DeleteOnFailure)
        {
            TryDelete(resumeStatePath);
        }
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

public sealed record ParallelDownloadItem(
    string Url,
    string DestinationPath,
    ParallelDownloadOptions? Options = null);

public sealed record ParallelDownloadResult(string Url, string? Path, Exception? Error)
{
    public bool IsSuccess => Error == null && Path != null;
}
