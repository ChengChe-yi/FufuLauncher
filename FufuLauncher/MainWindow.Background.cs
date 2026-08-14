/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Services;
using FufuLauncher.Services.Background;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Core;
using Windows.Media.Editing;
using Windows.Media.Playback;
using Windows.Storage;
using Windows.UI;

namespace FufuLauncher;

public sealed partial class MainWindow
{
    #region Background Management

    private async Task LoadBackgroundImageOpacityAsync()
    {
        try
        {
            var valueObj = await _localSettingsService.ReadSettingAsync("GlobalBackgroundImageOpacity");
            var opacity = 1.0;
            if (valueObj != null && double.TryParse(valueObj.ToString(), out var parsed)) opacity = parsed;
            ApplyBackgroundImageOpacity(opacity);
        }
        catch { ApplyBackgroundImageOpacity(1.0); }
    }

    private void ApplyBackgroundImageOpacity(double value)
    {
        var clamped = Math.Clamp(value, 0.0, 1.0);
        if (GlobalBackgroundImage != null) GlobalBackgroundImage.Opacity = clamped;
        if (GlobalBackgroundVideo != null) GlobalBackgroundVideo.Opacity = clamped;
    }

    private void UpdateBackgroundOverlayTheme()
    {
        try
        {
            if (_isExit) return;
            if (Content is not FrameworkElement rootElement) return;

            var currentTheme = rootElement.ActualTheme;
            if (currentTheme == ElementTheme.Default)
                currentTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;

            var themeBgColor = currentTheme == ElementTheme.Dark
                ? Color.FromArgb(255, 32, 32, 32)
                : Color.FromArgb(255, 243, 243, 243);

            GlobalBackgroundOverlay.Fill = new SolidColorBrush(themeBgColor);

            if (_isAcrylicOverlayEnabled && !_isVideoBackground)
            {
                PageBackgroundOverlay.Background = new AcrylicBrush
                {
                    TintColor = themeBgColor,
                    TintOpacity = 0.6,
                    FallbackColor = themeBgColor
                };
            }
            else
            {
                PageBackgroundOverlay.Background = new SolidColorBrush(themeBgColor);
            }

            ApplyFrameBackgroundOpacity(_frameBackgroundOpacity);
        }
        catch (ObjectDisposedException) { }
        catch (System.Runtime.InteropServices.COMException) { }
    }

    private async Task LoadGlobalBackgroundAsync()
    {
        try
        {
            var enabledJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.IsBackgroundEnabledKey);
            var isCustomEnabled = enabledJson == null ? true : Convert.ToBoolean(enabledJson);

            if (isCustomEnabled)
            {
                var isSlideshowEnabledJson = await _localSettingsService.ReadSettingAsync("IsBackgroundSlideshowEnabled");
                var isSlideshowEnabled = isSlideshowEnabledJson != null && Convert.ToBoolean(isSlideshowEnabledJson);

                if (isSlideshowEnabled)
                {
                    var slideshowFolderJson = await _localSettingsService.ReadSettingAsync("BackgroundSlideshowFolder");
                    var slideshowFolder = slideshowFolderJson?.ToString();

                    if (!string.IsNullOrEmpty(slideshowFolder) && Directory.Exists(slideshowFolder))
                    {
                        var slideshowIntervalJson = await _localSettingsService.ReadSettingAsync("BackgroundSlideshowInterval");
                        var interval = slideshowIntervalJson != null ? Convert.ToInt32(slideshowIntervalJson) : 60;
                        if (interval < 1) interval = 1;

                        await StartSlideshowAsync(slideshowFolder, interval);
                        return;
                    }
                }

                StopSlideshow();

                var customPathObj = await _localSettingsService.ReadSettingAsync("CustomBackgroundPath");
                var customPath = customPathObj?.ToString();

                if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                {
                    var customResult = await _backgroundRenderer.GetCustomBackgroundAsync(customPath);
                    if (customResult != null)
                    {
                        await ApplyGlobalBackgroundAsync(customResult);
                        return;
                    }
                }
            }
            else
            {
                StopSlideshow();
            }

            var preferVideoObj = await _localSettingsService.ReadSettingAsync("PreferVideoBackground");
            var preferVideo = preferVideoObj != null && Convert.ToBoolean(preferVideoObj) &&
                              _devBuildDetectionService.IsDevBuild;

            var serverJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.BackgroundServerKey);
            var serverValue = serverJson != null ? Convert.ToInt32(serverJson) : 0;
            var server = (ServerType)serverValue;

            var result = await _backgroundRenderer.GetBackgroundAsync(server, preferVideo);
            if (result != null)
                await ApplyGlobalBackgroundAsync(result);
        }
        catch
        {
            StopSlideshow();
            await ClearGlobalBackgroundAsync();
        }
    }

    private void StopSlideshow()
    {
        if (_slideshowTimer != null)
        {
            _slideshowTimer.Stop();
            _slideshowTimer = null;
        }
    }

    private async Task StartSlideshowAsync(string folder, int intervalSeconds)
    {
        StopSlideshow();

        try
        {
            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };
            _slideshowImages = Directory.GetFiles(folder)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (_slideshowImages.Count == 0)
            {
                await ClearGlobalBackgroundAsync();
                return;
            }

            _currentSlideshowIndex = 0;
            await ShowNextSlideshowImageAsync();

            _slideshowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(intervalSeconds) };
            _slideshowTimer.Tick += async (_, _) => await ShowNextSlideshowImageAsync();
            _slideshowTimer.Start();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动轮播失败: {ex.Message}");
        }
    }

    private async Task ShowNextSlideshowImageAsync()
    {
        if (_slideshowImages == null || _slideshowImages.Count == 0) return;

        if (_currentSlideshowIndex >= _slideshowImages.Count)
        {
            _currentSlideshowIndex = 0;
        }

        var imagePath = _slideshowImages[_currentSlideshowIndex];
        _currentSlideshowIndex++;

        var customResult = await _backgroundRenderer.GetCustomBackgroundAsync(imagePath);
        if (customResult != null)
        {
            await ApplyGlobalBackgroundAsync(customResult);
        }
    }

    private void DisposeGlobalBackgroundPlayer()
    {
        var player = _globalBackgroundPlayer;
        _globalBackgroundPlayer = null;
        _suspendedVideoSource = null;
        try
        {
            GlobalBackgroundVideo.SetMediaPlayer(null);
        }
        catch { }

        if (player != null)
        {
            if (_bgVideoFailedHandler != null)
            {
                player.MediaFailed -= _bgVideoFailedHandler;
            }
            player.Pause();
            player.Source = null;
            _ = Task.Run(() =>
            {
                try { player.Dispose(); } catch { }
            });
        }
        _bgVideoFailedHandler = null;
    }

    private async Task ApplyGlobalBackgroundAsync(BackgroundRenderResult? result)
    {
        if (result == null)
        {
            await ClearGlobalBackgroundAsync();
            return;
        }

        double finalOpacity = 1.0;
        try
        {
            var valueObj = await _localSettingsService.ReadSettingAsync("GlobalBackgroundImageOpacity");
            if (valueObj != null && double.TryParse(valueObj.ToString(), out var parsed))
            {
                finalOpacity = Math.Clamp(parsed, 0.0, 1.0);
            }
        }
        catch { }

        await RunOnUIThreadAsync(() =>
        {
            if (result.IsVideo)
            {
                _isVideoBackground = true;
                _bgVideoFallbackSource = null;
                GlobalBackgroundImage.Source = null;
                GlobalBackgroundImage.Visibility = Visibility.Collapsed;
                GlobalBackgroundVideo.Visibility = Visibility.Visible;

                if (_globalBackgroundPlayer == null)
                {
                    _globalBackgroundPlayer = MediaPlayerHelper.CreateLoopingMutedPlayer();
                    _bgVideoFailedHandler = OnGlobalBackgroundVideoFailed;
                    _globalBackgroundPlayer.MediaFailed += _bgVideoFailedHandler;
                    GlobalBackgroundVideo.SetMediaPlayer(_globalBackgroundPlayer);
                }
                if (!ReferenceEquals(_globalBackgroundPlayer.Source, result.VideoSource))
                {
                    _globalBackgroundPlayer.Pause();
                    _globalBackgroundPlayer.Source = null;
                    _globalBackgroundPlayer.Source = result.VideoSource;
                }

                if (_globalBackgroundPlayer.PlaybackSession.PlaybackState != MediaPlaybackState.Playing)
                {
                    _globalBackgroundPlayer.Play();
                }
            }
            else
            {
                var wasVideoBackground = _isVideoBackground;
                _isVideoBackground = false;
                DisposeGlobalBackgroundPlayer();
                GlobalBackgroundVideo.Visibility = Visibility.Collapsed;

                GlobalBackgroundImage.Opacity = 0.0;
                GlobalBackgroundImage.Visibility = Visibility.Visible;

                var anim = new DoubleAnimation
                {
                    From = 0.0,
                    To = finalOpacity,
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(anim, GlobalBackgroundImage);
                Storyboard.SetTargetProperty(anim, "Opacity");

                var storyboard = new Storyboard();
                storyboard.Children.Add(anim);

                bool isAnimationStarted = false;
                void StartFadeInAnimation()
                {
                    if (isAnimationStarted) return;
                    isAnimationStarted = true;
                    CleanupBgImageHandlers();
                    storyboard.Begin();
                }

                CleanupBgImageHandlers();

                _bgImageOpenedHandler = (s, e) => StartFadeInAnimation();
                _bgImageFailedHandler = (s, e) => StartFadeInAnimation();

                GlobalBackgroundImage.ImageOpened += _bgImageOpenedHandler;
                GlobalBackgroundImage.ImageFailed += _bgImageFailedHandler;

                GlobalBackgroundImage.Source = result.ImageSource;

                _bgFallbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
                _bgFallbackTimer.Tick += (s, e) =>
                {
                    _bgFallbackTimer.Stop();
                    StartFadeInAnimation();
                };
                _bgFallbackTimer.Start();

                if (wasVideoBackground)
                {
                    FlushMemory();
                }
            }

            UpdateBackgroundOverlayTheme();
        });
    }

    private void OnGlobalBackgroundVideoFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine($"背景视频播放失败({args.Error}): {args.ErrorMessage}");

        if (_isExit) return;

        var failedSource = sender.Source as MediaSource;
        if (failedSource == null || ReferenceEquals(failedSource, _bgVideoFallbackSource)) return;
        _bgVideoFallbackSource = failedSource;

        dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                await ShowVideoFirstFrameFallbackAsync(failedSource);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"视频首帧回退异常: {ex.Message}");
            }
            finally
            {
                if (ReferenceEquals(_bgVideoFallbackSource, failedSource))
                    _bgVideoFallbackSource = null;
            }
        });
    }

    private async Task ShowVideoFirstFrameFallbackAsync(MediaSource failedSource)
    {
        var videoPath = failedSource.Uri?.IsFile == true ? failedSource.Uri.LocalPath : null;

        if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
        {
            await ApplyFallbackBackgroundImageAsync(failedSource);
            return;
        }

        try
        {
            var file = await StorageFile.GetFileFromPathAsync(videoPath);
            var clip = await MediaClip.CreateFromFileAsync(file);
            var composition = new MediaComposition();
            composition.Clips.Add(clip);

            var thumbnail = await composition.GetThumbnailAsync(
                TimeSpan.Zero, 1920, 1080, VideoFramePrecision.NearestKeyFrame);

            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(thumbnail);

            if (!ReferenceEquals(_globalBackgroundPlayer?.Source, failedSource)) return;

            var opacity = GlobalBackgroundVideo.Opacity;

            _isVideoBackground = false;
            DisposeGlobalBackgroundPlayer();
            GlobalBackgroundVideo.Visibility = Visibility.Collapsed;

            GlobalBackgroundImage.Source = bitmap;
            GlobalBackgroundImage.Visibility = Visibility.Visible;
            GlobalBackgroundImage.Opacity = 0.0;

            var anim = new DoubleAnimation
            {
                From = 0.0,
                To = opacity,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };
            Storyboard.SetTarget(anim, GlobalBackgroundImage);
            Storyboard.SetTargetProperty(anim, "Opacity");

            var storyboard = new Storyboard();
            storyboard.Children.Add(anim);
            storyboard.Begin();

            UpdateBackgroundOverlayTheme();
            Debug.WriteLine("视频播放失败，已截取第一帧作为静态背景");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"提取视频首帧失败: {ex.Message}");
            await ApplyFallbackBackgroundImageAsync(failedSource);
        }
    }

    private async Task ApplyFallbackBackgroundImageAsync(MediaSource failedSource)
    {
        if (_isExit) return;

        var fallbackPath = Path.Combine(AppContext.BaseDirectory, "Assets", "bg.png");
        if (!File.Exists(fallbackPath))
        {
            Debug.WriteLine($"默认背景文件不存在: {fallbackPath}");
            return;
        }

        await RunOnUIThreadAsync(() =>
        {
            if (!ReferenceEquals(_globalBackgroundPlayer?.Source, failedSource)) return;

            var opacity = GlobalBackgroundVideo.Opacity;

            _isVideoBackground = false;
            DisposeGlobalBackgroundPlayer();
            GlobalBackgroundVideo.Visibility = Visibility.Collapsed;

            GlobalBackgroundImage.Source = new BitmapImage(new Uri(fallbackPath));
            GlobalBackgroundImage.Visibility = Visibility.Visible;
            GlobalBackgroundImage.Opacity = opacity;

            UpdateBackgroundOverlayTheme();
            Debug.WriteLine("视频播放失败，已回退至默认静态背景");
        });
    }

    private void CleanupBgImageHandlers()
    {
        _bgFallbackTimer?.Stop();
        if (_bgImageOpenedHandler != null)
        {
            GlobalBackgroundImage.ImageOpened -= _bgImageOpenedHandler;
            _bgImageOpenedHandler = null;
        }
        if (_bgImageFailedHandler != null)
        {
            GlobalBackgroundImage.ImageFailed -= _bgImageFailedHandler;
            _bgImageFailedHandler = null;
        }
    }

    private Task ClearGlobalBackgroundAsync()
    {
        return RunOnUIThreadAsync(() =>
        {
            GlobalBackgroundImage.Source = null;
            GlobalBackgroundImage.Visibility = Visibility.Collapsed;
            GlobalBackgroundVideo.Source = null;
            GlobalBackgroundVideo.Visibility = Visibility.Collapsed;
            DisposeGlobalBackgroundPlayer();
        });
    }

    #endregion
}
