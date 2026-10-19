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
        catch
        {
            ApplyBackgroundImageOpacity(1.0);
        }
    }

    private void ApplyBackgroundImageOpacity(double value)
    {
        StopBackgroundImageAnimation();
        var clamped = double.IsFinite(value) ? Math.Clamp(value, 0.0, 1.0) : 1.0;
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
                currentTheme = Application.Current.RequestedTheme == ApplicationTheme.Dark
                    ? ElementTheme.Dark
                    : ElementTheme.Light;

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
        catch (ObjectDisposedException)
        {
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
    }

    private async Task LoadGlobalBackgroundAsync()
    {
        var version = Interlocked.Increment(ref _backgroundLoadVersion);
        StopSlideshow();
        try
        {
            if (GlobalBackgroundImage.Source == null && !_isVideoBackground)
                await ApplyGlobalBackgroundAsync(await _backgroundRenderer.GetFallbackBackgroundAsync(), version);
            if (version != _backgroundLoadVersion || _isExit) return;

            var enabledJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.IsBackgroundEnabledKey);
            var isCustomEnabled = enabledJson == null ? true : Convert.ToBoolean(enabledJson);

            if (isCustomEnabled)
            {
                var isSlideshowEnabledJson =
                    await _localSettingsService.ReadSettingAsync("IsBackgroundSlideshowEnabled");
                var isSlideshowEnabled = isSlideshowEnabledJson != null && Convert.ToBoolean(isSlideshowEnabledJson);

                if (isSlideshowEnabled)
                {
                    var slideshowFolderJson = await _localSettingsService.ReadSettingAsync("BackgroundSlideshowFolder");
                    var slideshowFolder = slideshowFolderJson?.ToString();

                    if (!string.IsNullOrEmpty(slideshowFolder) && Directory.Exists(slideshowFolder))
                    {
                        var slideshowIntervalJson =
                            await _localSettingsService.ReadSettingAsync("BackgroundSlideshowInterval");
                        var interval = slideshowIntervalJson != null ? Convert.ToInt32(slideshowIntervalJson) : 60;
                        if (interval < 1) interval = 1;

                        if (await StartSlideshowAsync(slideshowFolder, interval, version)) return;
                    }
                }

                var customPathObj = await _localSettingsService.ReadSettingAsync("CustomBackgroundPath");
                var customPath = customPathObj?.ToString();

                if (!string.IsNullOrEmpty(customPath) && File.Exists(customPath))
                {
                    var customResult = await _backgroundRenderer.GetCustomBackgroundAsync(customPath);
                    if (customResult != null)
                    {
                        await ApplyGlobalBackgroundAsync(customResult, version);
                        return;
                    }
                }
            }

            var preferVideoObj = await _localSettingsService.ReadSettingAsync("PreferVideoBackground");
            var preferVideo = preferVideoObj != null && Convert.ToBoolean(preferVideoObj) &&
                              _devBuildDetectionService.IsDevBuild;

            var serverJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.BackgroundServerKey);
            var serverValue = serverJson != null ? Convert.ToInt32(serverJson) : 0;
            var server = (ServerType)serverValue;

            var result = await _backgroundRenderer.GetBackgroundAsync(server, preferVideo);
            if (result != null)
                await ApplyGlobalBackgroundAsync(result, version);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"全局背景加载失败: {ex}");
            if (version != _backgroundLoadVersion || _isExit) return;
            StopSlideshow();
            await ApplyGlobalBackgroundAsync(await _backgroundRenderer.GetFallbackBackgroundAsync(), version);
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

    private async Task<bool> StartSlideshowAsync(string folder, int intervalSeconds, int version)
    {
        if (version != _backgroundLoadVersion || _isExit) return true;
        StopSlideshow();

        try
        {
            string[] extensions = { ".jpg", ".jpeg", ".png", ".bmp" };
            _slideshowImages = Directory.GetFiles(folder)
                .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (_slideshowImages.Count == 0)
            {
                return false;
            }

            _currentSlideshowIndex = 0;
            if (!await ShowNextSlideshowImageAsync(version)) return false;
            if (version != _backgroundLoadVersion || _isExit) return true;

            _slideshowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(intervalSeconds) };
            _slideshowTimer.Tick += async (_, _) => await ShowNextSlideshowImageAsync(version);
            _slideshowTimer.Start();
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动轮播失败: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ShowNextSlideshowImageAsync(int version)
    {
        if (_slideshowLoadingVersion == version || version != _backgroundLoadVersion || _isExit) return false;
        _slideshowLoadingVersion = version;
        try
        {
            for (var attempt = 0; attempt < _slideshowImages.Count; attempt++)
            {
                if (version != _backgroundLoadVersion || _isExit) return false;
                if (_currentSlideshowIndex >= _slideshowImages.Count) _currentSlideshowIndex = 0;
                var imagePath = _slideshowImages[_currentSlideshowIndex++];
                var result = await _backgroundRenderer.GetCustomBackgroundAsync(imagePath);
                if (result == null) continue;
                await ApplyGlobalBackgroundAsync(result, version);
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"轮播图片加载失败: {ex.Message}");
            return false;
        }
        finally
        {
            if (_slideshowLoadingVersion == version) _slideshowLoadingVersion = null;
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
        catch
        {
        }

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
                try
                {
                    player.Dispose();
                }
                catch
                {
                }
            });
        }

        _bgVideoFailedHandler = null;
    }

    private async Task ApplyGlobalBackgroundAsync(BackgroundRenderResult? result, int? version = null,
        MediaSource? expectedVideoSource = null)
    {
        if (_isExit || (version.HasValue && version != _backgroundLoadVersion)) return;
        if (result == null)
        {
            return;
        }

        double finalOpacity = 1.0;
        try
        {
            var valueObj = await _localSettingsService.ReadSettingAsync("GlobalBackgroundImageOpacity");
            if (valueObj != null && double.TryParse(valueObj.ToString(), out var parsed) && double.IsFinite(parsed))
            {
                finalOpacity = Math.Clamp(parsed, 0.0, 1.0);
            }
        }
        catch
        {
        }

        await RunOnUIThreadAsync(() =>
        {
            if (_isExit || (version.HasValue && version != _backgroundLoadVersion)) return;
            if (expectedVideoSource != null &&
                !ReferenceEquals(_globalBackgroundPlayer?.Source, expectedVideoSource)) return;
            StopBackgroundImageAnimation();
            _isFallbackBackground = result.IsFallback;
            if (result.IsVideo)
            {
                _isVideoBackground = true;
                _bgVideoFallbackSource = null;
                GlobalBackgroundImage.Source = null;
                GlobalBackgroundImage.Visibility = Visibility.Collapsed;
                GlobalBackgroundVideo.Visibility = Visibility.Visible;
                GlobalBackgroundVideo.Opacity = finalOpacity;

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

                GlobalBackgroundImage.Source = result.ImageSource;

                GlobalBackgroundImage.Opacity = finalOpacity;
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

                _bgImageFadeStoryboard = new Storyboard();
                _bgImageFadeStoryboard.Children.Add(anim);
                try
                {
                    _bgImageFadeStoryboard.Begin();
                }
                catch (Exception ex)
                {
                    StopBackgroundImageAnimation();
                    Debug.WriteLine($"背景淡入动画不可用: {ex.Message}");
                }

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

        if (_isExit || !ReferenceEquals(sender, _globalBackgroundPlayer)) return;

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
        var version = _backgroundLoadVersion;
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

            await ApplyGlobalBackgroundAsync(new BackgroundRenderResult { ImageSource = bitmap }, version,
                failedSource);
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
        var version = _backgroundLoadVersion;
        var fallback = await _backgroundRenderer.GetFallbackBackgroundAsync();
        if (!ReferenceEquals(_globalBackgroundPlayer?.Source, failedSource)) return;
        await ApplyGlobalBackgroundAsync(fallback, version, failedSource);
    }

    private async void OnGlobalBackgroundImageFailed(object sender, ExceptionRoutedEventArgs args)
    {
        Debug.WriteLine($"全局背景显示失败: {args.ErrorMessage}");
        if (_isExit || _isFallbackBackground) return;
        _isFallbackBackground = true;
        var version = _backgroundLoadVersion;
        var failedSource = GlobalBackgroundImage.Source;
        try
        {
            var fallback = await _backgroundRenderer.GetFallbackBackgroundAsync();
            if (!ReferenceEquals(GlobalBackgroundImage.Source, failedSource)) return;
            await ApplyGlobalBackgroundAsync(fallback, version);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"全局背景显示回退失败: {ex}");
        }
    }

    private void StopBackgroundImageAnimation()
    {
        _bgImageFadeStoryboard?.Stop();
        _bgImageFadeStoryboard = null;
    }

    #endregion
}