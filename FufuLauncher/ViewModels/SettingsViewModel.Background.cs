/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Constants;
using FufuLauncher.Messages;
using FufuLauncher.Models;
using FufuLauncher.Services;
using FufuLauncher.Services.Background;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 背景设置

    private async Task ResetBackgroundApiAsync()
    {
        CustomBackgroundApiUrl = string.Empty;
        CurrentBackgroundApiUrl = GetDefaultBackgroundApiUrl(SelectedServer);
        await _localSettingsService.SaveSettingAsync("CustomBackgroundApiUrl", string.Empty);
        await _localSettingsService.SaveSettingAsync("BackgroundJsonHash", string.Empty);
        await _localSettingsService.SaveSettingAsync("SelectedOnlineBackgroundUrl", string.Empty);
        await _localSettingsService.SaveSettingAsync("SelectedOnlineBackgroundIsVideo", false);
        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
    }

    private static string GetDefaultBackgroundApiUrl(ServerType server)
    {
        return server switch
        {
            ServerType.CN => ApiEndpoints.BackgroundCnApi,
            ServerType.OS => ApiEndpoints.BackgroundOsApi,
            _ => ApiEndpoints.BackgroundCnApi
        };
    }

    private async Task ClearCustomBackgroundAsync()
    {
        try
        {
            await _localSettingsService.SaveSettingAsync<string>("CustomBackgroundPath", null);
            CustomBackgroundPath = null;
            HasCustomBackground = false;
    
            WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清除自定义背景失败: {ex.Message}");
        }
    }

    private async Task DownloadLatestBackgroundImageAsync()
    {
        try
        {
            var service = App.GetService<IHoyoverseBackgroundService>();
            var (imgUrl, _) = await service.GetLatestBackgroundUrlsAsync(SelectedServer);
            if (!string.IsNullOrEmpty(imgUrl))
            {
                await DownloadAndSaveFileAsync(imgUrl, "背景图片", ".png");
            }
            else
            {
                ShowDialogMessage("提示", "当前服务器没有可用的背景图片。");
            }
        }
        catch (Exception ex)
        {
            ShowDialogMessage("错误", $"下载图片失败: {ex.Message}");
        }
    }

    private async Task DownloadLatestBackgroundVideoAsync()
    {
        try
        {
            if (!_devBuildDetectionService.IsDevBuild)
            {
                ShowDialogMessage("提示", "动态背景仅开发版本可用，预览版与正式版本不支持下载背景视频");
                return;
            }

            var service = App.GetService<IHoyoverseBackgroundService>();
            var (_, videoUrl) = await service.GetLatestBackgroundUrlsAsync(SelectedServer);
            if (!string.IsNullOrEmpty(videoUrl))
            {
                await DownloadAndSaveFileAsync(videoUrl, "背景视频", GetUrlVideoExtension(videoUrl));
            }
            else
            {
                ShowDialogMessage("提示", "当前服务器没有可用的背景视频。");
            }
        }
        catch (Exception ex)
        {
            ShowDialogMessage("错误", $"下载视频失败: {ex.Message}");
        }
    }

    private static string GetUrlVideoExtension(string url)
    {
        try
        {
            var ext = Path.GetExtension(new Uri(url).AbsolutePath)?.ToLowerInvariant();
            if (ext is ".mp4" or ".webm" or ".mkv" or ".avi" or ".mov")
                return ext;
        }
        catch { }
        return ".mp4";
    }

    private async Task DownloadAndSaveFileAsync(string url, string typeName, string extension)
    {
        var isVideo = extension is ".mp4" or ".webm" or ".mkv" or ".avi" or ".mov";
        var filters = isVideo
            ? new[] { ("视频文件", new[] { extension }) }
            : new[] { ("图片文件", new[] { ".png", ".jpg" }) };
        var startLocation = isVideo
            ? Windows.Storage.Pickers.PickerLocationId.VideosLibrary
            : Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
        var defaultName = $"FufuLauncher_{typeName}_{DateTime.Now:yyyyMMddHHmmss}";

        var path = await FilePickerService.PickSaveFileAsync(
            null, filters, defaultName, startLocation,
            msg => ShowDialogMessage("错误", msg));
        if (string.IsNullOrEmpty(path)) return;

        using var httpClient = new HttpClient();
        var response = await httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write);
        await stream.CopyToAsync(fileStream);

        ShowDialogMessage("下载成功", $"{typeName} 已保存至：\n{path}");
    }

    private async void ShowDialogMessage(string title, string content)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = App.MainWindow.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
        catch { }
    }

    private async Task LoadCustomBackgroundSettingsAsync()
    {
        var path = await _localSettingsService.ReadSettingAsync("CustomBackgroundPath");
        if (path != null)
        {
            CustomBackgroundPath = path.ToString();
            HasCustomBackground = File.Exists(CustomBackgroundPath);
        }
        else
        {
            CustomBackgroundPath = null;
            HasCustomBackground = false;
        }

        var isSlideshowEnabledJson = await _localSettingsService.ReadSettingAsync("IsBackgroundSlideshowEnabled");
        IsBackgroundSlideshowEnabled = isSlideshowEnabledJson != null && Convert.ToBoolean(isSlideshowEnabledJson);

        var slideshowFolderJson = await _localSettingsService.ReadSettingAsync("BackgroundSlideshowFolder");
        if (slideshowFolderJson != null)
        {
            BackgroundSlideshowFolder = slideshowFolderJson.ToString();
            HasBackgroundSlideshowFolder = Directory.Exists(BackgroundSlideshowFolder);
        }
        else
        {
            BackgroundSlideshowFolder = null;
            HasBackgroundSlideshowFolder = false;
        }

        var slideshowIntervalJson = await _localSettingsService.ReadSettingAsync("BackgroundSlideshowInterval");
        if (slideshowIntervalJson != null)
        {
            BackgroundSlideshowInterval = Convert.ToInt32(slideshowIntervalJson);
        }
        else
        {
            BackgroundSlideshowInterval = 60;
        }
    }

    partial void OnCustomBackgroundApiUrlChanged(string value)
    {
        if (_isInitializing) return;
        var normalized = value?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(normalized) &&
            (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
             (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            return;
        }

        _ = _localSettingsService.SaveSettingAsync("CustomBackgroundApiUrl", normalized);
        CurrentBackgroundApiUrl = string.IsNullOrWhiteSpace(normalized) ? GetDefaultBackgroundApiUrl(SelectedServer) : normalized;
        _ = _localSettingsService.SaveSettingAsync("BackgroundJsonHash", string.Empty);
        _ = _localSettingsService.SaveSettingAsync("SelectedOnlineBackgroundUrl", string.Empty);
        _ = _localSettingsService.SaveSettingAsync("SelectedOnlineBackgroundIsVideo", false);
        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
    }

    partial void OnSelectedServerChanged(ServerType value)
    {
        if (_isInitializing) return;
        Debug.WriteLine($"SettingsViewModel: 保存服务器设置 {value}");
        CurrentBackgroundApiUrl = string.IsNullOrWhiteSpace(CustomBackgroundApiUrl) ? GetDefaultBackgroundApiUrl(value) : CustomBackgroundApiUrl;
        _ = _localSettingsService.SaveSettingAsync(LocalSettingsService.BackgroundServerKey, (int)value);
        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
    }

    partial void OnIsBackgroundSlideshowEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsBackgroundSlideshowEnabled", value);
        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
    }

    partial void OnBackgroundSlideshowIntervalChanged(int value)
    {
        if (_isInitializing) return;
        if (value < 1) value = 1; // min 1 second
        _ = _localSettingsService.SaveSettingAsync("BackgroundSlideshowInterval", value);
        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
    }

    partial void OnIsBackgroundEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        // Now means: whether custom background is allowed. If disabled, we fall back to official background.
        Debug.WriteLine($"SettingsViewModel: 保存自定义背景开关 {value}");
        _ = _localSettingsService.SaveSettingAsync(LocalSettingsService.IsBackgroundEnabledKey, value);

        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());

        if (!value)
        {
            _backgroundRenderer.ClearCustomBackground();
        }
    }

    private async Task SelectCustomBackgroundAsync()
    {
        try
        {
            var path = await _filePickerService.PickImageOrVideoAsync();
            if (!string.IsNullOrEmpty(path))
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                var isVideo = ext is ".mp4" or ".webm" or ".mkv" or ".avi" or ".mov";
                if (isVideo && !_devBuildDetectionService.IsDevBuild)
                {
                    ShowDialogMessage("提示", "动态背景仅开发版本可用，预览版与正式版本请选择图片作为自定义背景");
                    return;
                }

                CustomBackgroundPath = path;
                HasCustomBackground = true;
                await _localSettingsService.SaveSettingAsync("CustomBackgroundPath", path);

                WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
                await RefreshMainPageBackground();

            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"选择自定义背景失败: {ex.Message}");
        }
    }

    private async Task SelectBackgroundSlideshowFolderAsync()
    {
        try
        {
            var folder = await _filePickerService.PickFolderAsync();
            if (!string.IsNullOrEmpty(folder))
            {
                BackgroundSlideshowFolder = folder;
                HasBackgroundSlideshowFolder = true;
                await _localSettingsService.SaveSettingAsync("BackgroundSlideshowFolder", folder);

                WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"选择轮播图文件夹失败: {ex.Message}");
        }
    }

    private async Task ClearBackgroundSlideshowFolderAsync()
    {
        try
        {
            await _localSettingsService.SaveSettingAsync<string>("BackgroundSlideshowFolder", null);
            BackgroundSlideshowFolder = null;
            HasBackgroundSlideshowFolder = false;

            WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"清除轮播图文件夹失败: {ex.Message}");
        }
    }

    private async Task RefreshMainPageBackground()
    {
        await Task.CompletedTask;
    }

    #endregion
}
