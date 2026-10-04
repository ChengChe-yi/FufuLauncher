/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Models.Download;

/// <summary>下载失败原因，便于调用方按类别决定提示文案与是否重试。</summary>
public enum DownloadFailureReason
{
    /// <summary>未分类。</summary>
    Unknown = 0,

    /// <summary>HEAD 预检未通过（URL 不存在、无权限、网络不通）。</summary>
    PreFlightFailed,

    /// <summary>部分分片重试耗尽后仍失败。</summary>
    ChunksFailed,

    /// <summary>服务端不支持分段下载，且未允许降级。</summary>
    RangeNotSupported,

    /// <summary>下载结果与期望的哈希或大小不符。</summary>
    IntegrityMismatch,

    /// <summary>无数据（停滞）超时。</summary>
    Stalled,

    /// <summary>超过总时长上限。</summary>
    TimedOut,

    /// <summary>调用方主动取消。</summary>
    Cancelled,

    /// <summary>目标路径不可写（权限、占用、磁盘空间）。</summary>
    TargetUnwritable,
}

/// <summary>
/// 并行下载失败异常。携带结构化字段，调用方无需解析 <see cref="Exception.Message"/>。
/// </summary>
public sealed class DownloadFailedException : IOException
{
    /// <summary>源 URL。</summary>
    public string Url { get; }

    /// <summary>文件总大小（字节；0 表示未知）。</summary>
    public long TotalSize { get; }

    /// <summary>失败的小段数量。</summary>
    public int FailedChunks { get; }

    /// <summary>失败原因分类。</summary>
    public DownloadFailureReason Reason { get; }

    /// <summary>底层原始异常（若有）。</summary>
    public Exception? InnerError { get; }

    /// <summary>已成功写入的字节数，便于续传判断。</summary>
    public long BytesDownloaded { get; init; }

    public DownloadFailedException(
        string message,
        string url,
        long totalSize,
        int failedChunks,
        DownloadFailureReason reason,
        Exception? inner = null)
        : base(message, inner)
    {
        Url = url;
        TotalSize = totalSize;
        FailedChunks = failedChunks;
        Reason = reason;
        InnerError = inner;
    }
}
