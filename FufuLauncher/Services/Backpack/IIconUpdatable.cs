/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Services.Backpack;

internal interface IIconUpdatable
{
    BitmapImage? IconSource { set; }
}
