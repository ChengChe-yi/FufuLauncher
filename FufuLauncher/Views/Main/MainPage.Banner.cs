/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Numerics;
using FufuLauncher.Models;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Views;

public sealed partial class MainPage
{
    #region 轮播图

    private const double BannerSwipeThreshold = 42;
    private const double BannerAnimationMs = 520;
    private const double BannerInitialFadeMs = 560;
    private const double BannerInitialZoom = 1.08;
    private const float BannerClipCornerRadius = 12f;

    private BannerItem _displayedBanner;
    private BannerItem _pendingBanner;
    private bool _isBannerTransitioning;
    private bool _isBannerPointerPressed;
    private Windows.Foundation.Point _bannerPointerPressedPoint;

    private void InitializeBannerDisplay()
    {
        if (ViewModel.Banners == null || ViewModel.Banners.Count == 0)
        {
            BannerCurrentImage.Source = null;
            BannerIncomingImage.Source = null;
            _displayedBanner = null;
            _pendingBanner = null;
            ResetBannerLayers();
            BannerCurrentLayer.Opacity = 0;
            return;
        }

        if (ViewModel.CurrentBanner == null)
        {
            ViewModel.CurrentBanner = ViewModel.Banners[0];
            return;
        }

        TransitionToBanner(ViewModel.CurrentBanner, forceEntranceAnimation: true);
    }

    private void TransitionToBanner(BannerItem targetBanner, bool forceEntranceAnimation = false)
    {
        if (ViewModel == null)
        {
            return;
        }
        if (targetBanner == null)
        {
            return;
        }

        if (_isBannerTransitioning)
        {
            _pendingBanner = targetBanner;
            return;
        }

        if (!forceEntranceAnimation && ReferenceEquals(_displayedBanner, targetBanner) && BannerCurrentImage.Source != null)
        {
            return;
        }

        if (_displayedBanner == null || BannerCurrentImage.Source == null || forceEntranceAnimation)
        {
            ShowInitialBanner(targetBanner);
            return;
        }

        var direction = ResolveBannerDirection(_displayedBanner, targetBanner);
        StartBannerTransition(targetBanner, direction);
    }

    private void ShowInitialBanner(BannerItem targetBanner)
    {
        _displayedBanner = targetBanner;
        ResetBannerLayers();
        BannerCurrentLayer.Opacity = 0;
        BannerCurrentScale.ScaleX = BannerInitialZoom;
        BannerCurrentScale.ScaleY = BannerInitialZoom;

        SetBannerImage(BannerCurrentImage, targetBanner);
        FadeInInitialBanner();
    }

    private void FadeInInitialBanner()
    {
        BannerCurrentTranslate.X = 0;
        BannerIncomingTranslate.X = 0;
        BannerIncomingLayer.Opacity = 0;
        BannerCurrentLayer.Opacity = 0;
        BannerCurrentScale.ScaleX = BannerInitialZoom;
        BannerCurrentScale.ScaleY = BannerInitialZoom;

        var storyboard = new Storyboard();
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(BannerInitialFadeMs));

        storyboard.Children.Add(CreateDoubleAnimation(BannerCurrentLayer, "Opacity", 1, duration, easing));
        storyboard.Children.Add(CreateDoubleAnimation(BannerCurrentScale, "ScaleX", 1, duration, easing));
        storyboard.Children.Add(CreateDoubleAnimation(BannerCurrentScale, "ScaleY", 1, duration, easing));

        storyboard.Completed += (_, _) =>
        {
            if (!_isBannerTransitioning)
            {
                ResetBannerLayers();
            }
        };
        storyboard.Begin();
    }

    private int ResolveBannerDirection(BannerItem from, BannerItem to)
    {
        var count = ViewModel.Banners?.Count ?? 0;
        if (count < 2) return 1;

        var fromIndex = ViewModel.Banners.IndexOf(from);
        var toIndex = ViewModel.Banners.IndexOf(to);
        if (fromIndex < 0 || toIndex < 0) return 1;

        if ((fromIndex + 1) % count == toIndex) return 1;
        if ((fromIndex - 1 + count) % count == toIndex) return -1;

        return toIndex > fromIndex ? 1 : -1;
    }

    private void StartBannerTransition(BannerItem targetBanner, int direction)
    {
        var width = Math.Max(BannerViewport.ActualWidth, 1);
        var offset = width * 0.18 * direction;

        SetBannerImage(BannerIncomingImage, targetBanner);

        BannerIncomingTranslate.X = offset;
        BannerIncomingLayer.Opacity = 0;
        BannerIncomingScale.ScaleX = 1.015;
        BannerIncomingScale.ScaleY = 1.015;
        BannerCurrentTranslate.X = 0;
        BannerCurrentLayer.Opacity = 1;
        BannerCurrentScale.ScaleX = 1;
        BannerCurrentScale.ScaleY = 1;

        var storyboard = new Storyboard();
        var easing = new SineEase { EasingMode = EasingMode.EaseInOut };
        var duration = new Duration(TimeSpan.FromMilliseconds(BannerAnimationMs));

        storyboard.Children.Add(CreateDoubleAnimation(BannerCurrentTranslate, "X", -offset, duration, easing));
        storyboard.Children.Add(CreateDoubleAnimation(BannerCurrentLayer, "Opacity", 0, duration, easing));
        storyboard.Children.Add(CreateDoubleAnimation(BannerCurrentScale, "ScaleX", 0.985, duration, easing));
        storyboard.Children.Add(CreateDoubleAnimation(BannerCurrentScale, "ScaleY", 0.985, duration, easing));

        storyboard.Children.Add(CreateDoubleAnimation(BannerIncomingTranslate, "X", 0, duration, easing));
        storyboard.Children.Add(CreateDoubleAnimation(BannerIncomingLayer, "Opacity", 1, duration, easing));
        storyboard.Children.Add(CreateDoubleAnimation(BannerIncomingScale, "ScaleX", 1, duration, easing));
        storyboard.Children.Add(CreateDoubleAnimation(BannerIncomingScale, "ScaleY", 1, duration, easing));

        _isBannerTransitioning = true;
        storyboard.Completed += (_, _) =>
        {
            SwapBannerLayers(targetBanner);
            _isBannerTransitioning = false;

            if (_pendingBanner != null && !ReferenceEquals(_pendingBanner, _displayedBanner))
            {
                var pendingBanner = _pendingBanner;
                _pendingBanner = null;
                TransitionToBanner(pendingBanner);
            }
            else
            {
                _pendingBanner = null;
            }
        };
        storyboard.Begin();
    }

    private void SwapBannerLayers(BannerItem displayedBanner)
    {
        BannerCurrentImage.Source = BannerIncomingImage.Source;
        SetBannerPlaceholder(BannerCurrentImage, BannerIncomingPlaceholder.Visibility == Visibility.Visible);
        _displayedBanner = displayedBanner;
        ResetBannerLayers();
    }

    private void ResetBannerLayers()
    {
        BannerCurrentTranslate.X = 0;
        BannerIncomingTranslate.X = 0;
        BannerCurrentLayer.Opacity = 1;
        BannerIncomingLayer.Opacity = 0;
        BannerCurrentScale.ScaleX = 1;
        BannerCurrentScale.ScaleY = 1;
        BannerIncomingScale.ScaleX = 1;
        BannerIncomingScale.ScaleY = 1;
    }

    private void BannerPrev_Click(object sender, RoutedEventArgs e)
    {
        MoveBannerBy(-1);
    }

    private void BannerNext_Click(object sender, RoutedEventArgs e)
    {
        MoveBannerBy(1);
    }

    private void MoveBannerBy(int offset)
    {
        if (_isBannerTransitioning || ViewModel.Banners == null || ViewModel.Banners.Count < 2)
        {
            return;
        }

        var current = ViewModel.CurrentBanner ?? _displayedBanner ?? ViewModel.Banners[0];
        var currentIndex = ViewModel.Banners.IndexOf(current);
        if (currentIndex < 0) currentIndex = 0;

        var count = ViewModel.Banners.Count;
        var nextIndex = (currentIndex + offset + count) % count;
        ViewModel.CurrentBanner = ViewModel.Banners[nextIndex];
    }

    private void BannerViewport_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isBannerPointerPressed = true;
        _bannerPointerPressedPoint = e.GetCurrentPoint(BannerViewport).Position;
        BannerViewport.CapturePointer(e.Pointer);
    }

    private void BannerViewport_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isBannerPointerPressed)
        {
            return;
        }

        var releasedPoint = e.GetCurrentPoint(BannerViewport).Position;
        var deltaX = releasedPoint.X - _bannerPointerPressedPoint.X;
        _isBannerPointerPressed = false;
        BannerViewport.ReleasePointerCapture(e.Pointer);

        if (Math.Abs(deltaX) >= BannerSwipeThreshold)
        {
            MoveBannerBy(deltaX < 0 ? 1 : -1);
        }
    }

    private void BannerViewport_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        _isBannerPointerPressed = false;
    }

    private void BannerImage_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not Image imageControl) return;

        SetBannerPlaceholder(imageControl, false);
        if (ReferenceEquals(BannerCurrentImage.Source, BannerIncomingImage.Source))
        {
            SetBannerPlaceholder(BannerCurrentImage, false);
        }
    }

    private void BannerImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is not Image imageControl) return;

        SetBannerPlaceholder(imageControl, true);
        if (ReferenceEquals(BannerCurrentImage.Source, BannerIncomingImage.Source))
        {
            SetBannerPlaceholder(BannerCurrentImage, true);
        }
    }

    private void SetBannerImage(Image imageControl, BannerItem banner)
    {
        SetBannerPlaceholder(imageControl, true);
        imageControl.Source = null;

        if (string.IsNullOrWhiteSpace(banner?.Image?.Url)) return;

        try
        {
            imageControl.Source = new BitmapImage(new Uri(banner.Image.Url));
        }
        catch
        {
        }
    }

    private void SetBannerPlaceholder(Image imageControl, bool isVisible)
    {
        var placeholder = ReferenceEquals(imageControl, BannerCurrentImage)
            ? BannerCurrentPlaceholder
            : BannerIncomingPlaceholder;
        placeholder.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BannerImageArea_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateBannerRoundedClip();
    }

    private void UpdateBannerRoundedClip()
    {
        try
        {
            Visual visual = ElementCompositionPreview.GetElementVisual(BannerImageArea);
            Compositor compositor = visual.Compositor;

            CompositionRoundedRectangleGeometry geometry = compositor.CreateRoundedRectangleGeometry();
            geometry.CornerRadius = new Vector2(BannerClipCornerRadius, BannerClipCornerRadius);
            geometry.Size = new Vector2((float)BannerImageArea.ActualWidth, (float)BannerImageArea.ActualHeight);

            visual.Clip = compositor.CreateGeometricClip(geometry);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainPage] 设置轮播图圆角裁剪失败: {ex.Message}");
        }
    }

    private void BannerButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(ViewModel.CurrentBanner?.Image?.Link))
        {
            OpenLink(ViewModel.CurrentBanner.Image.Link);
        }
    }

    private void AnimateBannerArrowsOpacity(double toOpacity)
    {
        if (BannerPrevButton == null || BannerNextButton == null) return;

        var sb = new Storyboard();
        var duration = new Duration(TimeSpan.FromMilliseconds(200));

        var prevAnim = new DoubleAnimation { To = toOpacity, Duration = duration, EnableDependentAnimation = true };
        Storyboard.SetTarget(prevAnim, BannerPrevButton);
        Storyboard.SetTargetProperty(prevAnim, "Opacity");
        sb.Children.Add(prevAnim);

        var nextAnim = new DoubleAnimation { To = toOpacity, Duration = duration, EnableDependentAnimation = true };
        Storyboard.SetTarget(nextAnim, BannerNextButton);
        Storyboard.SetTargetProperty(nextAnim, "Opacity");
        sb.Children.Add(nextAnim);

        BannerPrevButton.IsHitTestVisible = toOpacity > 0;
        BannerNextButton.IsHitTestVisible = toOpacity > 0;

        sb.Begin();
    }

    #endregion
}
