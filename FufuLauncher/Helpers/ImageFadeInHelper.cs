#nullable enable

using System;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI.ViewManagement;

namespace FufuLauncher.Helpers;

public static class ImageFadeInHelper
{
    public static readonly DependencyProperty IsFadeInEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsFadeInEnabled",
            typeof(bool),
            typeof(ImageFadeInHelper),
            new PropertyMetadata(false, OnIsFadeInEnabledChanged));

    public static readonly DependencyProperty DurationMillisecondsProperty =
        DependencyProperty.RegisterAttached(
            "DurationMilliseconds",
            typeof(int),
            typeof(ImageFadeInHelper),
            new PropertyMetadata(350));

    public static readonly DependencyProperty EnableRetryProperty =
        DependencyProperty.RegisterAttached(
            "EnableRetry",
            typeof(bool),
            typeof(ImageFadeInHelper),
            new PropertyMetadata(true));

    public static readonly DependencyProperty MaxRetriesProperty =
        DependencyProperty.RegisterAttached(
            "MaxRetries",
            typeof(int),
            typeof(ImageFadeInHelper),
            new PropertyMetadata(3));

    public static readonly DependencyProperty RetryDelayMillisecondsProperty =
        DependencyProperty.RegisterAttached(
            "RetryDelayMilliseconds",
            typeof(int),
            typeof(ImageFadeInHelper),
            new PropertyMetadata(1200));

    public static readonly DependencyProperty FallbackSourceProperty =
        DependencyProperty.RegisterAttached(
            "FallbackSource",
            typeof(Microsoft.UI.Xaml.Media.ImageSource),
            typeof(ImageFadeInHelper),
            new PropertyMetadata(null));

    private static readonly ConditionalWeakTable<Image, ImageFadeInState> States = new();
    private static UISettings? _uiSettings;

    public static bool GetIsFadeInEnabled(DependencyObject obj) => (bool)obj.GetValue(IsFadeInEnabledProperty);

    public static void SetIsFadeInEnabled(DependencyObject obj, bool value) =>
        obj.SetValue(IsFadeInEnabledProperty, value);

    public static int GetDurationMilliseconds(DependencyObject obj) => (int)obj.GetValue(DurationMillisecondsProperty);

    public static void SetDurationMilliseconds(DependencyObject obj, int value) =>
        obj.SetValue(DurationMillisecondsProperty, value);

    public static bool GetEnableRetry(DependencyObject obj) => (bool)obj.GetValue(EnableRetryProperty);
    public static void SetEnableRetry(DependencyObject obj, bool value) => obj.SetValue(EnableRetryProperty, value);

    public static int GetMaxRetries(DependencyObject obj) => (int)obj.GetValue(MaxRetriesProperty);
    public static void SetMaxRetries(DependencyObject obj, int value) => obj.SetValue(MaxRetriesProperty, value);

    public static int GetRetryDelayMilliseconds(DependencyObject obj) =>
        (int)obj.GetValue(RetryDelayMillisecondsProperty);

    public static void SetRetryDelayMilliseconds(DependencyObject obj, int value) =>
        obj.SetValue(RetryDelayMillisecondsProperty, value);

    public static Microsoft.UI.Xaml.Media.ImageSource? GetFallbackSource(DependencyObject obj) =>
        (Microsoft.UI.Xaml.Media.ImageSource?)obj.GetValue(FallbackSourceProperty);

    public static void SetFallbackSource(DependencyObject obj, Microsoft.UI.Xaml.Media.ImageSource? value) =>
        obj.SetValue(FallbackSourceProperty, value);

    private static void OnIsFadeInEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Image image) return;

        bool isEnabled = (bool)e.NewValue;
        if (!isEnabled)
        {
            Detach(image);
            return;
        }

        Attach(image);
    }

    private static void Attach(Image image)
    {
        if (IsSpecialImage(image))
        {
            image.Opacity = 1.0;
            return;
        }

        var state = States.GetOrCreateValue(image);
        if (state.IsAttached) return;
        state.IsAttached = true;

        image.Loaded += OnImageLoaded;
        image.Unloaded += OnImageUnloaded;
        image.ImageOpened += OnImageOpened;
        image.ImageFailed += OnImageFailed;
        state.SourceCallbackToken = image.RegisterPropertyChangedCallback(Image.SourceProperty, OnSourceChanged);
        state.VisibilityCallbackToken =
            image.RegisterPropertyChangedCallback(UIElement.VisibilityProperty, OnVisibilityChanged);

        if (image.Source != null)
        {
            PrepareImageForLoading(image, state);
        }
    }

    private static void Detach(Image image)
    {
        if (States.TryGetValue(image, out var state))
        {
            state.IsAttached = false;
            state.SafetyTimer?.Stop();
            state.SafetyTimer = null;
            state.RetryTimer?.Stop();
            state.RetryTimer = null;
            state.CurrentStoryboard?.Stop();
            state.CurrentStoryboard = null;

            image.Loaded -= OnImageLoaded;
            image.Unloaded -= OnImageUnloaded;
            image.ImageOpened -= OnImageOpened;
            image.ImageFailed -= OnImageFailed;

            if (state.SourceCallbackToken != 0)
            {
                image.UnregisterPropertyChangedCallback(Image.SourceProperty, state.SourceCallbackToken);
                state.SourceCallbackToken = 0;
            }

            if (state.VisibilityCallbackToken != 0)
            {
                image.UnregisterPropertyChangedCallback(UIElement.VisibilityProperty, state.VisibilityCallbackToken);
                state.VisibilityCallbackToken = 0;
            }

            States.Remove(image);
        }

        image.Opacity = 1.0;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyProperty dp)
    {
        if (d is not Image image) return;
        if (!GetIsFadeInEnabled(image)) return;

        if (IsSpecialImage(image))
        {
            image.Opacity = 1.0;
            return;
        }

        if (States.TryGetValue(image, out var state))
        {
            PrepareImageForLoading(image, state);
        }
    }

    private static void OnVisibilityChanged(DependencyObject d, DependencyProperty dp)
    {
        if (d is not Image image) return;
        if (!GetIsFadeInEnabled(image)) return;

        if (image.Visibility == Visibility.Visible && States.TryGetValue(image, out var state))
        {
            if (state.HasOpened && image.Opacity < 1.0)
            {
                StartFadeInAnimation(image, state);
            }
        }
    }

    private static Uri? ExtractUri(ImageSource? source)
    {
        if (source is BitmapImage bmp)
        {
            return bmp.UriSource;
        }

        if (source is SvgImageSource svg)
        {
            return svg.UriSource;
        }

        return null;
    }

    private static void PrepareImageForLoading(Image image, ImageFadeInState state)
    {
        if (IsSpecialImage(image))
        {
            image.Opacity = 1.0;
            state.IsPendingOpen = false;
            return;
        }

        if (image.Source == null)
        {
            image.Opacity = 1.0;
            state.IsPendingOpen = false;
            state.HasOpened = false;
            state.RetryTimer?.Stop();
            state.RetryTimer = null;
            return;
        }

        if (ReferenceEquals(state.LastSource, image.Source))
        {
            return;
        }

        state.LastSource = image.Source;
        state.HasOpened = false;
        state.IsPendingOpen = true;

        if (!state.IsRetrying)
        {
            state.RetryCount = 0;
            state.RetryTimer?.Stop();
            state.RetryTimer = null;
            state.OriginalUri = ExtractUri(image.Source);
        }

        state.IsRetrying = false;

        image.Opacity = 0.0;

        state.SafetyTimer?.Stop();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3.5) };
        timer.Tick += (s, _) =>
        {
            timer.Stop();
            if (ReferenceEquals(state.SafetyTimer, timer))
            {
                state.SafetyTimer = null;
                if (state.IsPendingOpen && state.RetryTimer == null)
                {
                    state.IsPendingOpen = false;
                    image.Opacity = 1.0;
                }
            }
        };
        state.SafetyTimer = timer;
        timer.Start();

        if (image.Source is BitmapImage bmp && bmp.PixelWidth > 0)
        {
            state.HasOpened = true;
            if (image.IsLoaded && image.Visibility == Visibility.Visible)
            {
                StartFadeInAnimation(image, state);
            }
            else
            {
                image.Opacity = 1.0;
            }
        }
    }

    private static void OnImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image) return;

        if (IsSpecialImage(image))
        {
            image.Opacity = 1.0;
            return;
        }

        if (States.TryGetValue(image, out var state))
        {
            state.SafetyTimer?.Stop();
            state.SafetyTimer = null;
            state.RetryTimer?.Stop();
            state.RetryTimer = null;
            state.IsPendingOpen = false;
            state.HasOpened = true;
            state.RetryCount = 0;
            state.IsRetrying = false;

            if (image.IsLoaded && image.Visibility == Visibility.Visible)
            {
                StartFadeInAnimation(image, state);
            }
            else
            {
                image.Opacity = 1.0;
            }
        }
        else
        {
            image.Opacity = 1.0;
        }
    }

    private static void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is not Image image) return;

        if (!States.TryGetValue(image, out var state))
        {
            image.Opacity = 1.0;
            return;
        }

        state.SafetyTimer?.Stop();
        state.SafetyTimer = null;

        var uri = state.OriginalUri ?? ExtractUri(image.Source);
        bool isWebUri = uri != null && uri.IsAbsoluteUri &&
                        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        if (GetEnableRetry(image) && isWebUri && state.RetryCount < GetMaxRetries(image))
        {
            state.RetryCount++;
            int baseDelay = GetRetryDelayMilliseconds(image);
            int delay = baseDelay * state.RetryCount;

            state.RetryTimer?.Stop();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(delay) };
            timer.Tick += (s, _) =>
            {
                timer.Stop();
                if (!state.IsAttached || state.HasOpened) return;
                if (!ReferenceEquals(state.RetryTimer, timer)) return;
                state.RetryTimer = null;

                try
                {
                    state.IsRetrying = true;
                    var newBmp = new BitmapImage
                    {
                        CreateOptions = BitmapCreateOptions.IgnoreImageCache,
                        UriSource = uri
                    };
                    state.LastSource = newBmp;
                    image.Opacity = 0.0;
                    image.Source = newBmp;
                }
                catch
                {
                    state.IsRetrying = false;
                }
            };
            state.RetryTimer = timer;
            timer.Start();
            return;
        }

        state.IsPendingOpen = false;
        state.HasOpened = false;

        var fallback = GetFallbackSource(image);
        if (fallback != null && !ReferenceEquals(image.Source, fallback))
        {
            state.LastSource = fallback;
            image.Source = fallback;
            StartFadeInAnimation(image, state);
            return;
        }

        image.Opacity = 1.0;
    }

    private static void OnImageLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image) return;

        if (IsSpecialImage(image))
        {
            image.Opacity = 1.0;
            return;
        }

        if (States.TryGetValue(image, out var state))
        {
            if (state.HasOpened && image.Visibility == Visibility.Visible && image.Opacity < 1.0)
            {
                StartFadeInAnimation(image, state);
            }
            else if (state.IsPendingOpen && image.Source is BitmapImage bmp && bmp.PixelWidth > 0)
            {
                state.HasOpened = true;
                if (image.Visibility == Visibility.Visible)
                {
                    StartFadeInAnimation(image, state);
                }
            }
        }
    }

    private static void OnImageUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Image image) return;

        if (States.TryGetValue(image, out var state))
        {
            state.SafetyTimer?.Stop();
            state.SafetyTimer = null;
            state.RetryTimer?.Stop();
            state.RetryTimer = null;
            state.CurrentStoryboard?.Stop();
            state.CurrentStoryboard = null;
        }
    }

    private static void StartFadeInAnimation(Image image, ImageFadeInState state)
    {
        state.SafetyTimer?.Stop();
        state.SafetyTimer = null;
        state.RetryTimer?.Stop();
        state.RetryTimer = null;
        state.CurrentStoryboard?.Stop();
        state.CurrentStoryboard = null;

        if (!IsAnimationEnabled())
        {
            image.Opacity = 1.0;
            return;
        }

        int durationMs = GetDurationMilliseconds(image);
        if (durationMs <= 0)
        {
            image.Opacity = 1.0;
            return;
        }

        var animation = new DoubleAnimation
        {
            From = 0.0,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(durationMs),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, image);
        Storyboard.SetTargetProperty(animation, "Opacity");

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        state.CurrentStoryboard = storyboard;

        storyboard.Completed += (_, _) =>
        {
            image.Opacity = 1.0;
            if (ReferenceEquals(state.CurrentStoryboard, storyboard))
            {
                state.CurrentStoryboard = null;
            }
        };

        storyboard.Begin();
    }

    private static bool IsAnimationEnabled()
    {
        try
        {
            _uiSettings ??= new UISettings();
            return _uiSettings.AnimationsEnabled;
        }
        catch
        {
            return true;
        }
    }

    public static bool IsSpecialImage(Image image)
    {
        if (!GetIsFadeInEnabled(image))
        {
            return true;
        }

        var name = image.Name;
        if (!string.IsNullOrEmpty(name))
        {
            if (name is "TitleBarIcon"
                or "GlobalBackgroundImage"
                or "QrCodeImage"
                or "BannerCurrentImage"
                or "BannerIncomingImage"
                or "BgFallbackImage")
            {
                return true;
            }
        }

        if (image.Source is RenderTargetBitmap)
        {
            return true;
        }

        if (image.Source is BitmapImage bmp && bmp.UriSource != null)
        {
            var uri = bmp.UriSource.OriginalString;
            if (uri.Contains("WindowIcon.ico", StringComparison.OrdinalIgnoreCase) ||
                uri.Contains("WindowIcon.png", StringComparison.OrdinalIgnoreCase) ||
                uri.EndsWith("WindowIcon", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private sealed class ImageFadeInState
    {
        public bool IsAttached
        {
            get;
            set;
        }

        public object? LastSource
        {
            get;
            set;
        }

        public Uri? OriginalUri
        {
            get;
            set;
        }

        public bool IsPendingOpen
        {
            get;
            set;
        }

        public bool HasOpened
        {
            get;
            set;
        }

        public bool IsRetrying
        {
            get;
            set;
        }

        public int RetryCount
        {
            get;
            set;
        }

        public DispatcherTimer? SafetyTimer
        {
            get;
            set;
        }

        public DispatcherTimer? RetryTimer
        {
            get;
            set;
        }

        public long SourceCallbackToken
        {
            get;
            set;
        }

        public long VisibilityCallbackToken
        {
            get;
            set;
        }

        public Storyboard? CurrentStoryboard
        {
            get;
            set;
        }
    }
}