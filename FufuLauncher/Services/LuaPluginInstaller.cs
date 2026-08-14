/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text;
using FufuLauncher.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region Core & State

    private readonly PluginStoreService _storeService;
    private string _pluginsDir;
    private string? _expectedFileHash;
    private string? _expectedLuaHash;
    private string? _dlToken;
    private string? _accessToken;
    public event Action<DownloadProgressInfo>? ProgressChanged;
    public event Action<string>? LogReceived;

    public static DispatcherQueue? UIDispatcher { get; set; }

    public static XamlRoot? MainXamlRoot { get; set; }

    public List<string> CollectedLogs { get; } = new();

    public LuaPluginInstaller(PluginStoreService storeService)
    {
        _storeService = storeService;
        _pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
    }

    public void ClearCollectedLogs()
    {
        lock (CollectedLogs)
        {
            CollectedLogs.Clear();
        }
    }

    public void SaveLogsToFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        lock (CollectedLogs)
        {
            File.WriteAllLines(filePath, CollectedLogs, Encoding.UTF8);
        }
    }

    private void ReportProgress(double percent, string status, long bytesDownloaded = 0, long totalBytes = -1, long speed = 0)
    {
        var info = new DownloadProgressInfo
        {
            Percent = percent,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            SpeedBytesPerSecond = speed,
            StatusText = status
        };
        Debug.WriteLine($"[LuaInstaller] Progress {percent:F1}% ({FormatSize(bytesDownloaded)}/{FormatSize(totalBytes)} @ {FormatSpeed(speed)}): {status}");
        ProgressChanged?.Invoke(info);
    }

    private static string FormatSize(long bytes)
    {
        if (bytes <= 0) return "?";
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
        if (bytesPerSecond <= 0) return "?";
        return bytesPerSecond switch
        {
            >= 1_048_576 => $"{bytesPerSecond / 1_048_576.0:F1} MB/s",
            >= 1_024 => $"{bytesPerSecond / 1_024.0:F1} KB/s",
            _ => $"{bytesPerSecond} B/s"
        };
    }

    private void LogMessage(string message)
    {
        Debug.WriteLine($"[LuaInstaller] {message}");
        LogReceived?.Invoke(message);

        lock (CollectedLogs)
        {
            CollectedLogs.Add($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }
    }

    #endregion
}
