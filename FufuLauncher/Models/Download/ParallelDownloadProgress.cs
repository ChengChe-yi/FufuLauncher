/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Models.Download;

public sealed record ParallelDownloadProgress
{
    public long BytesDownloaded
    {
        get;
        init;
    }

    public long TotalBytes
    {
        get;
        init;
    }

    public double BytesPerSecond
    {
        get;
        init;
    }

    public int CompletedChunks
    {
        get;
        init;
    }

    public int TotalChunks
    {
        get;
        init;
    }

    public int ActiveSegments
    {
        get;
        init;
    }

    public bool IsCompleted
    {
        get;
        init;
    }

    public double Percent => TotalBytes > 0
        ? Math.Clamp(BytesDownloaded * 100.0 / TotalBytes, 0, 100)
        : 0;

    public TimeSpan? Eta => BytesPerSecond > 1 && TotalBytes > 0 && BytesDownloaded < TotalBytes
        ? TimeSpan.FromSeconds((TotalBytes - BytesDownloaded) / BytesPerSecond)
        : null;

    public DownloadProgressInfo ToProgressInfo(string? statusText = null) => new()
    {
        Percent = Percent,
        BytesDownloaded = BytesDownloaded,
        TotalBytes = TotalBytes,
        SpeedBytesPerSecond = (long)BytesPerSecond,
        StatusText = statusText ?? string.Empty,
    };
}