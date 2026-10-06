using FufuLauncher.Services.Miyoushe;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Views;

public sealed class MiyousheImageConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, string language) =>
        value is string url && CommunityContent.TryWebUri(url, out var uri) ? new BitmapImage(uri) : null;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}