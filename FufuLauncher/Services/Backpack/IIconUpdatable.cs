using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Services.Backpack;

internal interface IIconUpdatable
{
    BitmapImage? IconSource { set; }
}
