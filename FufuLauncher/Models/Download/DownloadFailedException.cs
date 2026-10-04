/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Models.Download;

public enum DownloadFailureReason
{
    Unknown = 0,

    PreFlightFailed,

    ChunksFailed,

    RangeNotSupported,

    IntegrityMismatch,

    Stalled,

    TimedOut,

    Cancelled,

    TargetUnwritable,
}

public sealed class DownloadFailedException : IOException
{
    public string Url { get; }

    public long TotalSize { get; }

    public int FailedChunks { get; }

    public DownloadFailureReason Reason { get; }

    public Exception? InnerError { get; }

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
