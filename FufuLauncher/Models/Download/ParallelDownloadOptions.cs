/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Models.Download;

public enum DownloadHashAlgorithm
{
    None = 0,

    Md5 = 1,

    XxHash64 = 2,
}

public sealed record ParallelDownloadOptions
{
    public const int MinSegments = 1;

    public const int MaxSegments = 32;

    // ------------------------------------------------------------ 并发

    public int MaxParallelSegments
    {
        get;
        init;
    } = 8;

    // 队列里的段数 = 分段数 × 本值，供工作线程竞争领取，实现负载均衡。
    public int ChunkMultiplier
    {
        get;
        init;
    } = 4;

    public long MinChunkSize
    {
        get;
        init;
    } = 128 * 1024;

    // 小于此值不走分段，避免多连接开销超过收益。
    public long MinParallelFileSize
    {
        get;
        init;
    } = 1024 * 1024;

    // <= 0 时按分段数自动推断。
    public int MaxConnectionsPerServer
    {
        get;
        init;
    }

    // ------------------------------------------------------------ 限速

    // 以下两项为 0 表示不限速。
    public long MaxBytesPerSecond
    {
        get;
        init;
    }

    public long MaxBytesPerSecondPerSegment
    {
        get;
        init;
    }

    // ------------------------------------------------------------ 重试

    public int MaxRetries
    {
        get;
        init;
    } = 3;

    public TimeSpan RetryBaseDelay
    {
        get;
        init;
    } = TimeSpan.FromSeconds(1);

    public TimeSpan RetryMaxDelay
    {
        get;
        init;
    } = TimeSpan.FromSeconds(30);

    // ------------------------------------------------------------ 超时

    public TimeSpan ConnectTimeout
    {
        get;
        init;
    } = TimeSpan.FromSeconds(15);

    public TimeSpan RequestTimeout
    {
        get;
        init;
    } = TimeSpan.FromSeconds(30);

    // 无数据达此时长即中断本次尝试并重试；0 表示关闭。
    public TimeSpan StallTimeout
    {
        get;
        init;
    } = TimeSpan.FromSeconds(30);

    // 整个下载的总时限；null 表示不限。
    public TimeSpan? OverallTimeout
    {
        get;
        init;
    }

    // ------------------------------------------------------------ 校验

    public string? ExpectedHash
    {
        get;
        init;
    }

    public DownloadHashAlgorithm HashAlgorithm
    {
        get;
        init;
    } = DownloadHashAlgorithm.None;

    // null 表示以服务端返回的大小为准。
    public long? ExpectedSize
    {
        get;
        init;
    }

    // ------------------------------------------------------------ 行为

    // 服务端不支持分段时是否降级为单线程。
    public bool AllowRangeFallback
    {
        get;
        init;
    } = true;

    // 先写 .part 临时文件，校验通过后再改名到目标路径。
    public bool UseTemporaryFile
    {
        get;
        init;
    } = true;

    public bool OverwriteExisting
    {
        get;
        init;
    } = true;

    // 启用后失败时保留 .part 与 .state.json，否则无法续传。
    public bool EnableResume
    {
        get;
        init;
    }

    public bool DeleteOnFailure
    {
        get;
        init;
    } = true;

    public string? UserAgent
    {
        get;
        init;
    }

    public int BufferSize
    {
        get;
        init;
    } = 81920;

    // 进度回调的最小间隔，避免高频上报拖慢 UI。
    public TimeSpan ProgressReportInterval
    {
        get;
        init;
    } = TimeSpan.FromMilliseconds(500);

    // ------------------------------------------------------------ 预设

    public static ParallelDownloadOptions Default
    {
        get;
    } = new();

    public static ParallelDownloadOptions Conservative
    {
        get;
    } = new()
    {
        MaxParallelSegments = 4,
        MaxBytesPerSecond = 2L * 1024 * 1024,
    };

    public static ParallelDownloadOptions Balanced
    {
        get;
    } = new()
    {
        MaxParallelSegments = 8,
        MaxBytesPerSecond = 5L * 1024 * 1024,
    };

    public static ParallelDownloadOptions Aggressive
    {
        get;
    } = new()
    {
        MaxParallelSegments = 16,
        ChunkMultiplier = 6,
        MinChunkSize = 256 * 1024,
    };

    public static ParallelDownloadOptions LowBandwidth
    {
        get;
    } = new()
    {
        MaxParallelSegments = 2,
        MaxBytesPerSecond = 256 * 1024,
        MaxBytesPerSecondPerSegment = 128 * 1024,
    };

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