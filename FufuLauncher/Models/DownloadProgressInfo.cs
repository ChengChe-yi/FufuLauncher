/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Models;

public class DownloadProgressInfo
{
    public double Percent { get; set; }
    public long BytesDownloaded { get; set; }
    public long TotalBytes { get; set; }
    public long SpeedBytesPerSecond { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public bool HasTotalSize => TotalBytes > 0;
    public string DownloadedSizeDisplay => FormatSize(BytesDownloaded);
    public string TotalSizeDisplay => HasTotalSize ? FormatSize(TotalBytes) : "???";
    public string SpeedDisplay => FormatSpeed(SpeedBytesPerSecond);
    public string PercentDisplay => $"{Percent:F1}%";
    private static string FormatSize(long bytes)
    {
        return bytes switch
        {
            >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
            >= 1_048_576 => $"{bytes / 1_048_576.0:F1} MB",
            >= 1_024 => $"{bytes / 1_024.0:F1} KB",
            _ => $"{bytes} B"
        };
    }
    private static string FormatSpeed(long bytesPerSecond)
    {
        if (bytesPerSecond <= 0) return "—";
        return bytesPerSecond switch
        {
            >= 1_048_576 => $"{bytesPerSecond / 1_048_576.0:F1} MB/s",
            >= 1_024 => $"{bytesPerSecond / 1_024.0:F1} KB/s",
            _ => $"{bytesPerSecond} B/s"
        };
    }
}
