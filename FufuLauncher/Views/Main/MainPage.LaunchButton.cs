/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;

namespace FufuLauncher.Views;

public sealed partial class MainPage
{
    #region 启动按钮与页脚悬停动效

    private void LaunchButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimateLaunchButtonHoverOpacity(1.0);
    }

    private void LaunchButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimateLaunchButtonHoverOpacity(0.0);
    }

    private void AnimateLaunchButtonHoverOpacity(double targetOpacity)
    {
        if (LaunchButtonHoverLayer == null) return;

        var storyboard = new Storyboard();
        var duration = new Duration(TimeSpan.FromMilliseconds(200));
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };

        storyboard.Children.Add(CreateDoubleAnimation(LaunchButtonHoverLayer, "Opacity", targetOpacity, duration, easing));

        storyboard.Begin();
    }

    private void AnimateLaunchButtonOverlay(double toOpacity)
    {
        if (LaunchButtonOverlayBorder.Opacity == toOpacity) return;

        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = new Duration(TimeSpan.FromSeconds(1.5)),

            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        Storyboard.SetTarget(animation, LaunchButtonOverlayBorder);
        Storyboard.SetTargetProperty(animation, "Opacity");

        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void Copyright_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        AnimateCopyrightOpacity(0.8);
    }

    private void Copyright_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        AnimateCopyrightOpacity(0.05);
    }

    private void AnimateCopyrightOpacity(double toOpacity)
    {
        var storyboard = new Storyboard();
        var animation = new DoubleAnimation
        {
            To = toOpacity,
            Duration = new Duration(TimeSpan.FromMilliseconds(300)),
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, CopyrightText);
        Storyboard.SetTargetProperty(animation, "Opacity");

        storyboard.Children.Add(animation);
        storyboard.Begin();
    }

    private void ScreenshotButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {

    }

    private void ScreenshotButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {

    }

    #endregion
}
