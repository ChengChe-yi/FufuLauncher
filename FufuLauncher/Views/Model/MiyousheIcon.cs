using FufuLauncher.Services.Miyoushe;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace FufuLauncher.Views;

public sealed class MiyousheIcon : UserControl
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string),
        typeof(MiyousheIcon), new PropertyMetadata("Like", OnKindChanged));

    private readonly Microsoft.UI.Xaml.Shapes.Path _path = new() { Width = 24, Height = 24, StrokeThickness = 1.8,
        StrokeLineJoin = PenLineJoin.Round, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };

    public MiyousheIcon()
    {
        Width = Height = 14;
        VerticalAlignment = VerticalAlignment.Center;
        _path.SetBinding(Shape.StrokeProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Foreground)) });
        Content = new Viewbox { Child = _path };
        UpdatePath();
    }

    public string Kind
    {
        get => (string)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static void OnKindChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) => ((MiyousheIcon)sender).UpdatePath();

    private void UpdatePath() => _path.Data = (Geometry)XamlBindingHelper.ConvertValue(typeof(Geometry), Kind switch
    {
        "Eye" => CommunityIconPaths.Eye,
        "Comment" => CommunityIconPaths.Comment,
        _ => CommunityIconPaths.Like
    });
}
