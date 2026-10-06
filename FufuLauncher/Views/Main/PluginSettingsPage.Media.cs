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
}