/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FufuLauncher.Views;

public sealed partial class BBSWindow
{
    #region 顶栏与导航

    private void ToggleTopBar()
    {
        TopBarGrid.Visibility = TopBarGrid.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private void RootGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Tab)
        {
            ToggleTopBar();
            e.Handled = true;
        }
    }

    private void GoButton_Click(object sender, RoutedEventArgs e) => NavigateToUrl();
    private void UrlTextBox_KeyDown(object sender, KeyRoutedEventArgs e) { if (e.Key == Windows.System.VirtualKey.Enter) NavigateToUrl(); }

    private void NavigateToUrl()
    {
        var url = UrlTextBox.Text;
        if (!string.IsNullOrEmpty(url) && !url.StartsWith("http")) url = "https://" + url;
        if (!string.IsNullOrEmpty(url)) BBSWebView.CoreWebView2.Navigate(url);
    }

    private void ClientTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BBSWebView == null) return;
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item && item.Tag is string type)
        {
            if (_clientConfigs.TryGetValue(type, out var config))
            {
                _currentConfig = config;
                UpdateWebViewSettings();
                BBSWebView.Reload();
            }
        }
    }

    #endregion
}
