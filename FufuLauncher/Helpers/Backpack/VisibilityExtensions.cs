using Microsoft.UI.Xaml;

namespace FufuLauncher.Helpers;

internal static class VisibilityExt
{
    internal static Visibility ToVisibility(this bool value) =>
        value ? Visibility.Visible : Visibility.Collapsed;

    internal static Visibility ToCollapsed(this bool value) =>
        value ? Visibility.Collapsed : Visibility.Visible;
}
