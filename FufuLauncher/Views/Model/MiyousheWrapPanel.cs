using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace FufuLauncher.Views;

public sealed class MiyousheWrapPanel : Panel
{
    private const double Gap = 8;

    protected override Size MeasureOverride(Size availableSize)
    {
        double x = 0, y = 0, rowHeight = 0, width = 0;
        foreach (var child in Children.Where(c => c.Visibility == Visibility.Visible))
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > availableSize.Width)
            {
                x = 0;
                y += rowHeight + Gap;
                rowHeight = 0;
            }

            width = Math.Max(width, x + size.Width);
            x += size.Width + Gap;
            rowHeight = Math.Max(rowHeight, size.Height);
        }

        return new Size(Math.Ceiling(width), Math.Ceiling(y + rowHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, rowHeight = 0;
        foreach (var child in Children.Where(c => c.Visibility == Visibility.Visible))
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > finalSize.Width + 0.5)
            {
                x = 0;
                y += rowHeight + Gap;
                rowHeight = 0;
            }

            child.Arrange(new Rect(x, y, size.Width, size.Height));
            x += size.Width + Gap;
            rowHeight = Math.Max(rowHeight, size.Height);
        }

        return finalSize;
    }
}