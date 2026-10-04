/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Models.Download;

/// <summary>
/// 并行下载进度快照。不可变，可安全跨线程/跨 UI 边界传递。
/// </summary>
public sealed record ParallelDownloadProgress
{
    /// <summary>已下载字节数（含各分片累计）。</summary>
    public long BytesDownloaded { get; init; }

    /// <summary>文件总字节数；0 表示未知。</summary>
    public long TotalBytes { get; init; }

    /// <summary>瞬时速度（字节/秒）。</summary>
    public double BytesPerSecond { get; init; }

    /// <summary>已完成的小段数。</summary>
    public int CompletedChunks { get; init; }

    /// <summary>小段总数。</summary>
    public int TotalChunks { get; init; }

    /// <summary>当前活跃的连接数。</summary>
    public int ActiveSegments { get; init; }

    /// <summary>是否已全部完成。</summary>
    public bool IsCompleted { get; init; }

    /// <summary>完成百分比（0–100）；总大小未知时为 0。</summary>
    public double Percent => TotalBytes > 0
        ? Math.Clamp(BytesDownloaded * 100.0 / TotalBytes, 0, 100)
        : 0;

    /// <summary>预计剩余时间；无法估算时为 null。</summary>
    public TimeSpan? Eta => BytesPerSecond > 1 && TotalBytes > 0 && BytesDownloaded < TotalBytes
        ? TimeSpan.FromSeconds((TotalBytes - BytesDownloaded) / BytesPerSecond)
        : null;

    /// <summary>转换为通用的进度模型，便于复用现有 UI 绑定。</summary>
    public DownloadProgressInfo ToProgressInfo(string? statusText = null) => new()
    {
        Percent = Percent,
        BytesDownloaded = BytesDownloaded,
        TotalBytes = TotalBytes,
        SpeedBytesPerSecond = (long)BytesPerSecond,
        StatusText = statusText ?? string.Empty,
    };
}
