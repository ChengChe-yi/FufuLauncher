using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.ViewModels;

public sealed record SubStatItemViewModel(
    string      Name,
    string      ValueDisplay,
    BitmapImage BadgeSource
);
