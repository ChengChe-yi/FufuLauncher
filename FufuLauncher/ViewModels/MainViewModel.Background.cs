/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Services;
using FufuLauncher.Services.Background;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

namespace FufuLauncher.ViewModels;

public partial class MainViewModel
{
    #region 背景管理
    [ObservableProperty] private ImageSource _backgroundImageSource;
    [ObservableProperty] private MediaPlayer _backgroundVideoPlayer;
    private InMemoryRandomAccessStream _backgroundVideoStream;
    [ObservableProperty] private bool _isVideoBackground;
    [ObservableProperty] private bool _isBackgroundLoading;

    [ObservableProperty] private string _customBackgroundPath;
    [ObservableProperty] private bool _hasCustomBackground;

    [ObservableProperty] private ObservableCollection<BackgroundUrlInfo> _availableBackgrounds = new();
    public IAsyncRelayCommand<BackgroundUrlInfo> SelectSpecificBackgroundCommand { get; }

    [ObservableProperty] private bool _preferVideoBackground = true;

    [ObservableProperty] private bool _isBackgroundToggleEnabled = true;

    public Visibility ImageVisibility => IsVideoBackground ? Visibility.Collapsed : Visibility.Visible;
    public Visibility VideoVisibility => IsVideoBackground ? Visibility.Visible : Visibility.Collapsed;
    public string BackgroundTypeToggleText => "切换背景";

    public IAsyncRelayCommand LoadBackgroundCommand
    {
        get;
    }

    public IRelayCommand ToggleBackgroundTypeCommand
    {
        get;
    }

    partial void OnIsVideoBackgroundChanged(bool value)
    {
        OnPropertyChanged(nameof(ImageVisibility));
        OnPropertyChanged(nameof(VideoVisibility));
    }

    partial void OnHasCustomBackgroundChanged(bool value)
    {
        IsBackgroundToggleEnabled = !value;
    }

    private async Task LoadAvailableBackgroundsAsync()
    {
        try
        {
            var serverJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.BackgroundServerKey);
            int serverValue = serverJson != null ? Convert.ToInt32(serverJson) : 0;
            var server = (Models.ServerType)serverValue;

            var backgroundService = App.GetService<IHoyoverseBackgroundService>();
            var backgrounds = await backgroundService.GetAvailableBackgroundsAsync(server);

            var visibleBackgrounds = backgrounds
                .Where(b => _devBuildDetectionService.IsDevBuild || !b.IsVideo)
                .ToList();

            await UpdateUI(() =>
            {
                AvailableBackgrounds.Clear();
                foreach (var bg in visibleBackgrounds)
                {
                    AvailableBackgrounds.Add(bg);
                }
            });

            var imageUrls = backgrounds
                .Where(b => !b.IsVideo && !string.IsNullOrEmpty(b.Url))
                .Select(b => b.Url);
            _ = _backgroundRenderer.PreloadImageBackgroundsAsync(imageUrls);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载可选背景失败: {ex.Message}");
        }
    }

    private async Task SelectSpecificBackgroundAsync(BackgroundUrlInfo info)
    {
        if (info == null) return;

        if (info.IsVideo && !_devBuildDetectionService.IsDevBuild)
        {
            _notificationService.Show("动态背景不可用", "预览版与正式版本不支持动态背景", NotificationType.Warning, 5000);
            return;
        }

        await _localSettingsService.SaveSettingAsync("SelectedOnlineBackgroundUrl", info.Url);
        await _localSettingsService.SaveSettingAsync("SelectedOnlineBackgroundIsVideo", info.IsVideo);

        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
    }

    public async Task LoadCustomBackgroundPathAsync()
    {
        var path = await _localSettingsService.ReadSettingAsync("CustomBackgroundPath");
        if (path != null)
        {
            CustomBackgroundPath = path.ToString();
            HasCustomBackground = File.Exists(CustomBackgroundPath);
        }
        else
        {
            HasCustomBackground = false;
        }

        IsBackgroundToggleEnabled = !HasCustomBackground;
    }

    private async Task SwitchToStaticBackgroundOnVersionChangeAsync()
    {
        try
        {
            var currentVersion = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "";
            var lastVersion = await _localSettingsService.ReadSettingAsync("LastAppVersion");
            string lastVersionStr = lastVersion?.ToString() ?? "";

            if (!string.IsNullOrEmpty(lastVersionStr) && lastVersionStr != currentVersion)
            {
                if (PreferVideoBackground)
                {
                    PreferVideoBackground = false;
                    await _localSettingsService.SaveSettingAsync("PreferVideoBackground", false);
                    await _localSettingsService.SaveSettingAsync("UserPreferVideoBackground", false);
                    Debug.WriteLine($"[MainViewModel] 版本更变 ({lastVersionStr} -> {currentVersion})，已将动态背景切换为静态背景");
                }
            }

            await _localSettingsService.SaveSettingAsync("LastAppVersion", currentVersion);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainViewModel] 版本变更背景切换检查失败: {ex.Message}");
        }
    }

    private async Task LoadBackgroundAsync()
    {
        await UpdateUI(() => IsBackgroundLoading = true);
        ClearBackground();

        try
        {
            if (HasCustomBackground && !string.IsNullOrEmpty(CustomBackgroundPath) && File.Exists(CustomBackgroundPath))
            {
                await UpdateUI(() => TryLoadImage(CustomBackgroundPath));
            }
            else
            {
                var serverJson = await _localSettingsService.ReadSettingAsync("BackgroundServerKey");
                var server = Models.ServerType.CN;
                try { if (serverJson != null) server = (Models.ServerType)Convert.ToInt32(serverJson); } catch { }

                var preferVideo = PreferVideoBackground && _devBuildDetectionService.IsDevBuild;
                var bgResult = await _backgroundRenderer.GetBackgroundAsync(server, preferVideo);

                await UpdateUI(() =>
                {
                    if (bgResult != null)
                    {
                        if (bgResult.IsVideo && bgResult.VideoSource != null && _devBuildDetectionService.IsDevBuild)
                        {
                            SetupVideoPlayer(bgResult.VideoSource, bgResult.VideoStream);
                        }
                        else if (!bgResult.IsVideo && bgResult.ImageSource != null)
                        {
                            BackgroundImageSource = bgResult.ImageSource;
                            IsVideoBackground = false;
                        }
                        else
                        {
                            LoadFallbackImage();
                        }
                    }
                    else
                    {
                        LoadFallbackImage();
                    }
                });
            }
        }
        catch (NotSupportedException ex) when (ex.Message == "IMAGE_DECODE_FAILED")
        {
            await UpdateUI(() =>
            {
                _notificationService.Show("背景解码失败", "系统缺少 WebP 图像扩展。已回退至静态背景。", NotificationType.Error, 6000);
                LoadFallbackImage();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"背景加载异常: {ex.Message}");
            await UpdateUI(LoadFallbackImage);
        }
        finally
        {
            await UpdateUI(() => IsBackgroundLoading = false);
        }
    }

    private void SetupVideoPlayer(MediaSource source, InMemoryRandomAccessStream stream)
    {
        _backgroundVideoStream = stream;

        if (BackgroundVideoPlayer == null)
        {
            BackgroundVideoPlayer = MediaPlayerHelper.CreateLoopingMutedPlayer();
            BackgroundVideoPlayer.MediaFailed += BackgroundVideoPlayer_MediaFailed;
        }
        BackgroundVideoPlayer.Source = source;
        BackgroundVideoPlayer.Play();
        IsVideoBackground = true;
    }

    private void ClearBackground()
    {
        BackgroundImageSource = null;
        if (BackgroundVideoPlayer != null)
        {
            BackgroundVideoPlayer.Pause();
            BackgroundVideoPlayer.MediaFailed -= BackgroundVideoPlayer_MediaFailed;
            try
            {
                BackgroundVideoPlayer.Dispose();
            }
            catch { }
            BackgroundVideoPlayer = null;
        }
        _backgroundVideoStream?.Dispose();
        _backgroundVideoStream = null;
        IsVideoBackground = false;
    }

    private void BackgroundVideoPlayer_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine($"背景视频触发MediaFailed，错误类型: {args.Error}");
    }

    private void TryLoadImage(string path)
    {
        try
        {
            var bitmap = new BitmapImage();

            bitmap.UriSource = new Uri(path);

            bitmap.ImageFailed += (_, _) =>
            {
                Debug.WriteLine($"图片解码失败: {path}，正在切换至默认背景。");
                _dispatcherQueue.TryEnqueue(LoadFallbackImage);
            };

            BackgroundImageSource = bitmap;
            IsVideoBackground = false;
        }
        catch
        {
            LoadFallbackImage();
        }
    }

    private void LoadFallbackImage()
    {
        try
        {
            string fallbackPath = Path.Combine(AppContext.BaseDirectory, "Assets", "bg.png");

            if (File.Exists(fallbackPath))
            {
                if (BackgroundImageSource is BitmapImage currentBmp &&
                    currentBmp.UriSource?.LocalPath == fallbackPath)
                {
                    return;
                }

                var bitmap = new BitmapImage();
                bitmap.UriSource = new Uri(fallbackPath);
                BackgroundImageSource = bitmap;
                IsVideoBackground = false;
                Debug.WriteLine("已加载默认背景: Assets/bg.png");
            }
            else
            {
                Debug.WriteLine($"严重错误: 默认背景文件不存在 -> {fallbackPath}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"加载默认背景失败: {ex.Message}");
        }
    }

    private void ToggleBackgroundType()
    {
        if (!_devBuildDetectionService.IsDevBuild)
        {
            _notificationService.Show("动态背景不可用", "预览版与正式版本不支持动态背景", NotificationType.Warning, 5000);
            return;
        }

        PreferVideoBackground = !PreferVideoBackground;
        OnPropertyChanged(nameof(BackgroundTypeToggleText));
        _ = _localSettingsService.SaveSettingAsync("UserPreferVideoBackground", PreferVideoBackground);
        _ = _localSettingsService.SaveSettingAsync("PreferVideoBackground", PreferVideoBackground);
        WeakReferenceMessenger.Default.Send(new BackgroundRefreshMessage());
    }
    #endregion
}
