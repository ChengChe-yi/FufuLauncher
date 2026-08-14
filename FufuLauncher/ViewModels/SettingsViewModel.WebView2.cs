/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region WebView2 缓存管理

    private string FormatSize(long bytes)
    {
        if (bytes == 0) return "0 B";

        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        int counter = 0;
        decimal number = bytes;

        while (Math.Round(number / 1024) >= 1)
        {
            number /= 1024;
            counter++;
        }
        return string.Format("{0:n2} {1}", number, suffixes[counter]);
    }

    private async void UpdateWebView2CacheSizeAsync(bool forceRefresh = false)
    {
        try
        {
            string cacheFolder = Path.Combine(AppContext.BaseDirectory, "FufuLauncher.exe.WebView2");
            if (!Directory.Exists(cacheFolder))
            {
                WebView2CacheSize = "0 MB";
                return;
            }

            if (!forceRefresh && _cachedWebView2CacheSize != null)
            {
                WebView2CacheSize = _cachedWebView2CacheSize;
                return;
            }

            long size = await Task.Run(() => GetDirectorySize(new DirectoryInfo(cacheFolder)));
            var formatted = FormatSize(size);
            _cachedWebView2CacheSize = formatted;
            WebView2CacheSize = formatted;
        }
        catch
        {
            WebView2CacheSize = "未知大小";
        }
    }

    private long GetDirectorySize(DirectoryInfo d)
    {
        long size = 0;
        try
        {
            FileInfo[] fis = d.GetFiles();
            foreach (FileInfo fi in fis)
            {
                size += fi.Length;
            }
            
            DirectoryInfo[] dis = d.GetDirectories();
            foreach (DirectoryInfo di in dis)
            {
                size += GetDirectorySize(di);
            }
        }
        catch
        {
            // ignored
        }

        return size;
    }

    private async Task ClearWebView2CacheAsync()
    {
        try
        {
            var cacheFolder = Path.Combine(AppContext.BaseDirectory, "FufuLauncher.exe.WebView2");
    
            if (Directory.Exists(cacheFolder))
            {
                await Task.Run(() => SafeDeleteDirectory(cacheFolder));
            }
            
            UpdateWebView2CacheSizeAsync(true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清除 WebView2 缓存失败: {ex.Message}");
        }
    }

    private void SafeDeleteDirectory(string targetDir)
    {
        try
        {
            var files = Directory.GetFiles(targetDir);
            var dirs = Directory.GetDirectories(targetDir);
            
            foreach (var file in files)
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch
                {
                    // ignored
                }
            }
            
            foreach (var dir in dirs)
            {
                SafeDeleteDirectory(dir);
                try
                {
                    Directory.Delete(dir, false);
                }
                catch
                {
                    // ignored
                }
            }
        }
        catch
        {
            // ignored
        }
    }

    #endregion
}
