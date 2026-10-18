/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace FufuLauncher.Views;

public sealed partial class GachaAnalysisWindow
{
    private void PlayOverviewTransition(UIElement target) => PlaySectionTransition(target);

    private void PlaySectionTransition(UIElement? target)
    {
        if (target == null) return;

        if (target.RenderTransform is not CompositeTransform transform)
        {
            transform = new CompositeTransform();
            target.RenderTransform = transform;
            target.RenderTransformOrigin = new Point(0.5, 0.5);
        }

        _sectionTransitionStoryboard?.Stop();

        target.Opacity = 0;
        transform.TranslateY = 16;
        transform.ScaleX = 0.99;
        transform.ScaleY = 0.99;

        var storyboard = new Storyboard();
        AddViewModeAnimation(storyboard, target, "Opacity", 0, 1, 0, 260);
        AddViewModeAnimation(storyboard, transform, "TranslateY", 16, 0, 0, 320);
        AddViewModeAnimation(storyboard, transform, "ScaleX", 0.99, 1, 0, 320);
        AddViewModeAnimation(storyboard, transform, "ScaleY", 0.99, 1, 0, 320);
        _sectionTransitionStoryboard = storyboard;
        storyboard.Begin();
    }

    private static void AddViewModeAnimation(Storyboard storyboard, DependencyObject target, string property,
        double from, double to, int delayMs, int durationMs)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    private void PlayAnalysisChartAnimations()
    {
        if (!ViewModel.ShowAnalysisContent) return;

        _analysisChartStoryboard?.Stop();
        _analysisChartStoryboard = new Storyboard();

        AddPieAnimation(RarityPieChart, RarityPieScale, 0);
        AddPieAnimation(PoolPieChart, PoolPieScale, 80);
        AddChartAnimation(RecentFiveStarChart, 130);
        AddChartAnimation(FourStarTopChart, 190);
        AddChartAnimation(PityBucketsChart, 250);
        AddChartAnimation(MonthlyPullsChart, 310);

        _analysisChartStoryboard.Begin();
    }

    private void AddPieAnimation(UIElement element, ScaleTransform scale, int delayMs)
    {
        if (element == null || scale == null) return;

        element.Opacity = 0;
        scale.ScaleX = 0.96;
        scale.ScaleY = 0.96;

        AddDoubleAnimation(element, "Opacity", 0, 1, delayMs, 260);
        AddDoubleAnimation(scale, "ScaleX", 0.96, 1.235, delayMs, 360);
        AddDoubleAnimation(scale, "ScaleY", 0.96, 1.235, delayMs, 360);
    }

    private void AddChartAnimation(UIElement element, int delayMs)
    {
        if (element == null) return;

        if (element.RenderTransform is not CompositeTransform transform)
        {
            transform = new CompositeTransform();
            element.RenderTransform = transform;
        }

        element.Opacity = 0;
        transform.TranslateY = 18;
        transform.ScaleY = 0.94;

        AddDoubleAnimation(element, "Opacity", 0, 1, delayMs, 240);
        AddDoubleAnimation(transform, "TranslateY", 18, 0, delayMs, 340);
        AddDoubleAnimation(transform, "ScaleY", 0.94, 1, delayMs, 340);
    }

    private void AddDoubleAnimation(DependencyObject target, string property, double from, double to, int delayMs,
        int durationMs)
    {
        var animation = new DoubleAnimation
        {
            From = from,
            To = to,
            BeginTime = TimeSpan.FromMilliseconds(delayMs),
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        _analysisChartStoryboard.Children.Add(animation);
    }

    private void OnChartScrollViewerLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            StretchChartContentToViewport(scrollViewer);
        }
    }

    private void OnChartScrollViewerSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            StretchChartContentToViewport(scrollViewer);
        }
    }

    private static void StretchChartContentToViewport(ScrollViewer scrollViewer)
    {
        if (scrollViewer.Content is FrameworkElement content)
        {
            content.MinWidth = Math.Max(0, scrollViewer.ActualWidth);
            content.MinHeight = Math.Max(0, scrollViewer.ActualHeight);
        }
    }

    private void OnHorizontalScrollViewerPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableWidth <= 0) return;

        var delta = e.GetCurrentPoint(scrollViewer).Properties.MouseWheelDelta;
        if (delta == 0) return;

        var nextOffset = Math.Clamp(scrollViewer.HorizontalOffset - delta, 0, scrollViewer.ScrollableWidth);
        scrollViewer.ChangeView(nextOffset, null, null, true);
        e.Handled = true;
    }
}
