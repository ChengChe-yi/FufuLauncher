using FufuLauncher.Services.Miyoushe;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Views;

public sealed class MiyousheAvatar : Button
{
    public static readonly DependencyProperty AvatarProperty = DependencyProperty.Register(nameof(Avatar), typeof(string),
        typeof(MiyousheAvatar), new PropertyMetadata("", OnAvatarChanged));
    public static readonly DependencyProperty DisplayNameProperty = DependencyProperty.Register(nameof(DisplayName), typeof(string),
        typeof(MiyousheAvatar), new PropertyMetadata("", OnDisplayNameChanged));

    private readonly PersonPicture _picture = new();

    public MiyousheAvatar()
    {
        MinWidth = MinHeight = 0;
        Padding = BorderThickness = new Thickness(0);
        CornerRadius = new CornerRadius(100);
        Background = new SolidColorBrush(Colors.Transparent);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        _picture.SetBinding(WidthProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Width)) });
        _picture.SetBinding(HeightProperty, new Binding { Source = this, Path = new PropertyPath(nameof(Height)) });
        Content = _picture;
    }

    public string Avatar { get => (string)GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
    public string DisplayName { get => (string)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }

    private static void OnAvatarChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (MiyousheAvatar)sender;
        control._picture.ProfilePicture = null;
        if (!CommunityContent.TryWebUri(args.NewValue as string ?? "", out var uri)) return;
        var image = new BitmapImage();
        image.ImageFailed += (_, _) => { if (control._picture.ProfilePicture == image) control._picture.ProfilePicture = null; };
        control._picture.ProfilePicture = image;
        image.UriSource = uri;
    }

    private static void OnDisplayNameChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var control = (MiyousheAvatar)sender;
        control._picture.DisplayName = args.NewValue as string ?? "";
        AutomationProperties.SetName(control, control._picture.DisplayName);
        ToolTipService.SetToolTip(control, control._picture.DisplayName);
    }
}
