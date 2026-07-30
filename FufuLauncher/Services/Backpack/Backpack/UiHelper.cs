/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

internal static class UiHelper
{
    internal static void ShowDetailFlyout(FrameworkElement anchor, string title, string content, double maxWidth = 380)
    {
        if (string.IsNullOrEmpty(content)) return;
        var res   = Application.Current.Resources;
        var panel = new StackPanel { MaxWidth = maxWidth, Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text         = title,
            Style        = res["BodyStrongTextBlockStyle"] as Style,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        panel.Children.Add(new TextBlock
        {
            Text         = content,
            Style        = res["CaptionTextBlockStyle"] as Style,
            Foreground   = res["TextFillColorSecondaryBrush"] as Brush,
            TextWrapping = TextWrapping.WrapWholeWords
        });
        new Flyout { Content = panel }.ShowAt(anchor);
    }
}
