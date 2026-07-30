/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Services.Backpack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

public sealed class DeferredImage : Grid
{
    public static readonly DependencyProperty SourceUriProperty = DependencyProperty.Register(
        nameof(SourceUri), typeof(Uri), typeof(DeferredImage), new PropertyMetadata(null, OnSourceChanged));

    public static readonly DependencyProperty DecodePixelWidthProperty = DependencyProperty.Register(
        nameof(DecodePixelWidth), typeof(int), typeof(DeferredImage), new PropertyMetadata(0, OnSourceChanged));

    private readonly Image _image = new()
    {
        Stretch = Stretch.Uniform,
        IsHitTestVisible = false,
        Source = GfxLoader.Placeholder
    };

    private CancellationTokenSource? _loadCts;

    public Uri? SourceUri
    {
        get => (Uri?)GetValue(SourceUriProperty);
        set => SetValue(SourceUriProperty, value);
    }

    public int DecodePixelWidth
    {
        get => (int)GetValue(DecodePixelWidthProperty);
        set => SetValue(DecodePixelWidthProperty, value);
    }

    public DeferredImage()
    {
        Children.Add(_image);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DeferredImage image)
            image.BeginLoad();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => BeginLoad();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
    }

    private async void BeginLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var token = _loadCts.Token;
        var uri = SourceUri;
        _image.Source = GfxLoader.Placeholder;
        if (uri is null) return;

        try
        {
            var source = await GfxLoader.LoadAsync(uri, DecodePixelWidth, token);
            if (!token.IsCancellationRequested && uri == SourceUri)
                _image.Source = source;
        }
        catch (OperationCanceledException)
        {
        }
    }
}
