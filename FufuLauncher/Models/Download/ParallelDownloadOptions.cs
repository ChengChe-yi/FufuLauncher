/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Models.Download;

/// <summary>下载校验使用的哈希算法。</summary>
public enum DownloadHashAlgorithm
{
    /// <summary>不校验。</summary>
    None = 0,

    /// <summary>MD5（32 位小写 hex）。</summary>
    Md5 = 1,

    /// <summary>XxHash64（16 位小写 hex）。</summary>
    XxHash64 = 2,
}

/// <summary>
/// 并行下载参数。所有字段都有安全默认值，<see cref="Validate"/> 会再做一次收敛，
/// 因此调用方可以只覆写关心的项。
/// </summary>
public sealed record ParallelDownloadOptions
{
    /// <summary>并发分段数的下限。</summary>
    public const int MinSegments = 1;

    /// <summary>并发分段数的上限。</summary>
    public const int MaxSegments = 32;

    // ------------------------------------------------------------ 并发

    /// <summary>并行分段（工作线程）数，范围 1–32。1 表示单线程。</summary>
    public int MaxParallelSegments { get; init; } = 8;

    /// <summary>
    /// 小段放大倍数：队列里放入 <c>MaxParallelSegments × ChunkMultiplier</c> 个小段，
    /// 由工作线程竞争领取，使快的连接自动多干活。
    /// </summary>
    public int ChunkMultiplier { get; init; } = 4;

    /// <summary>单个小段的最小字节数，避免产生过多 HTTP 请求。</summary>
    public long MinChunkSize { get; init; } = 128 * 1024;

    /// <summary>小于该大小直接走单线程，收益不足以抵消多连接开销。</summary>
    public long MinParallelFileSize { get; init; } = 1024 * 1024;

    /// <summary>单服务器最大连接数；&lt;= 0 表示按分段数自动推断。</summary>
    public int MaxConnectionsPerServer { get; init; }

    // ------------------------------------------------------------ 限速

    /// <summary>全局下载限速（字节/秒）；0 表示不限速。</summary>
    public long MaxBytesPerSecond { get; init; }

    /// <summary>单连接限速（字节/秒）；0 表示不限速。</summary>
    public long MaxBytesPerSecondPerSegment { get; init; }

    // ------------------------------------------------------------ 重试

    /// <summary>单个小段的最大重试次数。</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>重试退避基数，按 <c>基数 × 2^尝试次数</c> 递增。</summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>重试退避上限。</summary>
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromSeconds(30);

    // ------------------------------------------------------------ 超时

    /// <summary>建立连接超时。</summary>
    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>单次请求（含 HEAD 预检）的超时上限。</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>无数据（停滞）超时，超过则中断本次尝试并重试；&lt;= 0 表示关闭。</summary>
    public TimeSpan StallTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>整个下载的总时长上限；null 表示不限制。</summary>
    public TimeSpan? OverallTimeout { get; init; }

    // ------------------------------------------------------------ 校验

    /// <summary>期望的哈希值（与 <see cref="HashAlgorithm"/> 配套）。</summary>
    public string? ExpectedHash { get; init; }

    /// <summary>哈希算法，<see cref="DownloadHashAlgorithm.None"/> 表示不校验。</summary>
    public DownloadHashAlgorithm HashAlgorithm { get; init; } = DownloadHashAlgorithm.None;

    /// <summary>期望的文件大小；null 表示以服务端返回为准。</summary>
    public long? ExpectedSize { get; init; }

    // ------------------------------------------------------------ 行为

    /// <summary>服务端不支持 Range 时是否降级为单线程流式下载。</summary>
    public bool AllowRangeFallback { get; init; } = true;

    /// <summary>是否先写临时文件、校验通过后再改名到目标路径。</summary>
    public bool UseTemporaryFile { get; init; } = true;

    /// <summary>目标文件已存在时是否覆盖；false 则直接报错。</summary>
    public bool OverwriteExisting { get; init; } = true;

    /// <summary>
    /// 是否启用断点续传（复用已有的 <c>.part</c> 与已完成分片记录 <c>.state.json</c>）。
    /// <para><b>启用后失败时不会清理这两者</b>（见 <see cref="DeleteOnFailure"/>），
    /// 否则续传无从谈起。若续传记录与 <c>.part</c> 的实际长度不符，记录会被自动丢弃重下。</para>
    /// </summary>
    public bool EnableResume { get; init; }

    /// <summary>
    /// 失败时是否清理临时文件与续传记录。
    /// <para>当 <see cref="EnableResume"/> 为 true 时该项对 <c>.part</c> 与记录不生效 ——
    /// 要续传就必须保留它们。另外，<see cref="UseTemporaryFile"/> 为 false 时
    /// 临时文件就是目标文件本身，此时任何情况都不会删除它，以免破坏调用方的原有文件。</para>
    /// </summary>
    public bool DeleteOnFailure { get; init; } = true;

    /// <summary>自定义 User-Agent；null 表示使用进程默认。</summary>
    public string? UserAgent { get; init; }

    /// <summary>读写缓冲区字节数。</summary>
    public int BufferSize { get; init; } = 81920;

    /// <summary>进度回调的最小间隔，避免高频上报拖慢 UI。</summary>
    public TimeSpan ProgressReportInterval { get; init; } = TimeSpan.FromMilliseconds(500);

    // ------------------------------------------------------------ 预设

    /// <summary>默认：8 分段、不限速。</summary>
    public static ParallelDownloadOptions Default { get; } = new();

    /// <summary>保守：4 分段、全局 2 MB/s，适合带宽紧张或对端限流较严。</summary>
    public static ParallelDownloadOptions Conservative { get; } = new()
    {
        MaxParallelSegments = 4,
        MaxBytesPerSecond = 2L * 1024 * 1024,
    };

    /// <summary>均衡：8 分段、全局 5 MB/s。</summary>
    public static ParallelDownloadOptions Balanced { get; } = new()
    {
        MaxParallelSegments = 8,
        MaxBytesPerSecond = 5L * 1024 * 1024,
    };

    /// <summary>激进：16 分段、不限速，适合大文件且网络良好。</summary>
    public static ParallelDownloadOptions Aggressive { get; } = new()
    {
        MaxParallelSegments = 16,
        ChunkMultiplier = 6,
        MinChunkSize = 256 * 1024,
    };

    /// <summary>低带宽：2 分段、全局 256 KB/s，避免把上行也占满。</summary>
    public static ParallelDownloadOptions LowBandwidth { get; } = new()
    {
        MaxParallelSegments = 2,
        MaxBytesPerSecond = 256 * 1024,
        MaxBytesPerSecondPerSegment = 128 * 1024,
    };

    /// <summary>把各项收敛到合法范围；非法组合会抛出 <see cref="ArgumentException"/>。</summary>
    public ParallelDownloadOptions Validate()
    {
        var o = this with
        {
            MaxParallelSegments = Math.Clamp(MaxParallelSegments, MinSegments, MaxSegments),
            ChunkMultiplier = Math.Clamp(ChunkMultiplier, 1, 16),
            MinChunkSize = Math.Max(MinChunkSize, 16 * 1024),
            BufferSize = Math.Clamp(BufferSize, 4 * 1024, 1024 * 1024),
            MaxRetries = Math.Max(MaxRetries, 0),
            RetryBaseDelay = RetryBaseDelay < TimeSpan.Zero ? TimeSpan.Zero : RetryBaseDelay,
            MinParallelFileSize = Math.Max(MinParallelFileSize, 0),
            MaxBytesPerSecond = Math.Max(MaxBytesPerSecond, 0),
            MaxBytesPerSecondPerSegment = Math.Max(MaxBytesPerSecondPerSegment, 0),
            StallTimeout = StallTimeout < TimeSpan.Zero ? TimeSpan.Zero : StallTimeout,
            ProgressReportInterval = ProgressReportInterval < TimeSpan.FromMilliseconds(50)
                ? TimeSpan.FromMilliseconds(50)
                : ProgressReportInterval,
        };

        if (o.RetryMaxDelay < o.RetryBaseDelay)
        {
            o = o with { RetryMaxDelay = o.RetryBaseDelay };
        }

        if (o.MaxConnectionsPerServer <= 0)
        {
            o = o with { MaxConnectionsPerServer = Math.Max(o.MaxParallelSegments, 8) };
        }

        if (o.HashAlgorithm != DownloadHashAlgorithm.None && string.IsNullOrWhiteSpace(o.ExpectedHash))
        {
            throw new ArgumentException(
                "指定了 HashAlgorithm 但未提供 ExpectedHash。", nameof(ExpectedHash));
        }

        if (o.ExpectedSize is <= 0)
        {
            o = o with { ExpectedSize = null };
        }

        return o;
    }
}
