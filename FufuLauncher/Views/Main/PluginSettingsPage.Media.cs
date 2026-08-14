/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage
{
    private void HelpImage_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Image img && img.Parent is Grid grid)
        {
            if (grid.FindName("LoadingRing") is ProgressRing loadingRing)
            {
                loadingRing.IsActive = false;
                loadingRing.Visibility = Visibility.Collapsed;
            }
        }
    }

    private void HelpImage_ImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Image img && img.Parent is Grid grid)
        {
            img.Visibility = Visibility.Collapsed;
        
            if (grid.FindName("LoadingRing") is ProgressRing loadingRing)
            {
                loadingRing.IsActive = false;
                loadingRing.Visibility = Visibility.Collapsed;
            }
        
            if (grid.FindName("ErrorText") is TextBlock errorText)
            {
                errorText.Visibility = Visibility.Visible;
            }
        }
    }

    private void AvatarPreview_ImageOpened(object sender, RoutedEventArgs e)
    {
        if (sender is Microsoft.UI.Xaml.Controls.Image image)
        {
            var storyboard = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)),
                BeginTime = TimeSpan.FromSeconds(0.5),
                EasingFunction = new Microsoft.UI.Xaml.Media.Animation.CubicEase 
                { 
                    EasingMode = Microsoft.UI.Xaml.Media.Animation.EasingMode.EaseOut 
                }
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, image);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Opacity");
            storyboard.Children.Add(animation);
            storyboard.Begin();
        }
    }
}
