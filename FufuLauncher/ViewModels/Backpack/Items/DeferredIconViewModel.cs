/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Services.Backpack;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.ViewModels;

public abstract class DeferredIconViewModel : ObservableObject, IIconUpdatable
{
    private BitmapImage? _iconSource;
    private int _iconLoadStarted;

    protected DeferredIconViewModel(Uri? iconUri, int decodePixelWidth)
    {
        IconUri = iconUri;
        DecodePixelWidth = decodePixelWidth;
    }

    public Uri? IconUri { get; }

    public int DecodePixelWidth { get; }

    public BitmapImage? IconSource
    {
        get
        {
            EnsureIconLoadStarted();
            return _iconSource ?? GfxLoader.Placeholder;
        }
        private set => SetProperty(ref _iconSource, value);
    }

    BitmapImage? IIconUpdatable.IconSource
    {
        set => IconSource = value;
    }

    public async Task<BitmapImage?> GetIconAsync(
        int decodePixelWidth = 0,
        int decodePixelHeight = 0,
        CancellationToken cancellationToken = default)
    {
        if (IconUri is null)
            return null;

        var image = await GfxLoader.GetAsync(
            IconUri,
            decodePixelWidth > 0 ? decodePixelWidth : DecodePixelWidth,
            decodePixelHeight,
            cancellationToken);
        if (image is not null)
            IconSource = image;
        return image;
    }

    private void EnsureIconLoadStarted()
    {
        if (IconUri is null || Interlocked.Exchange(ref _iconLoadStarted, 1) != 0)
            return;

        GfxLoader.BeginLoad(IconUri, this, DecodePixelWidth);
    }
}
