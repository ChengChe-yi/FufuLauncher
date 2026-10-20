/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Services.CycleChallenge;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Views.Controls.CycleChallenge;

public sealed partial class RemoteImage : UserControl
{
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(RemoteImage),
        new PropertyMetadata(string.Empty, OnSourceChanged));


    public string? Source
    {
        get => (string?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    private CancellationTokenSource? _loadCts;
    private int _loadVersion;
    private string _displayedSource = string.Empty;
    private Uri? _currentRemoteUri;

    public RemoteImage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ContentImage.ImageOpened += OnImageOpened;
        ContentImage.ImageFailed += OnImageFailed;
        SizeChanged += OnSizeChanged;
        UpdatePlaceholderGlyphSize();
    }

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is RemoteImage control)
        {
            control.Reload(e.NewValue as string);
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        string current = Source?.Trim() ?? string.Empty;


        if (ContentImage.Source is not null && string.Equals(_displayedSource, current, StringComparison.Ordinal))
        {
            return;
        }

        Reload(Source);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelPending();
        _loadVersion++;
    }


    private void Reload(string? source)
    {
        CancelPending();
        int version = ++_loadVersion;

        string trimmed = source?.Trim() ?? string.Empty;
        _displayedSource = trimmed;
        _currentRemoteUri = null;


        if (!IsLoaded)
        {
            ShowPlaceholder();
            return;
        }

        if (trimmed.Length == 0)
        {
            ShowPlaceholder();
            return;
        }

        _ = LoadAsync(trimmed, version);
    }

    private async Task LoadAsync(string source, int version)
    {
        var cts = new CancellationTokenSource();
        _loadCts = cts;
        CancellationToken token = cts.Token;

        try
        {
            if (TryBuildLocalUri(source, out Uri? localUri))
            {
                if (version != _loadVersion || token.IsCancellationRequested)
                {
                    return;
                }

                SetBitmap(localUri!, version);
                return;
            }

            if (!Uri.TryCreate(source, UriKind.Absolute, out Uri? remoteUri) || !IsHttpUri(remoteUri))
            {
                Debug.WriteLine($"[RemoteImage] 不支持的图片来源: {source}");
                ShowPlaceholder();
                return;
            }

            _currentRemoteUri = remoteUri;
            string? cachedPath = await RemoteImageCache.GetAsync(remoteUri, token);

            if (version != _loadVersion || token.IsCancellationRequested)
            {
                return;
            }

            if (string.IsNullOrEmpty(cachedPath))
            {
                ShowPlaceholder();
                return;
            }

            SetBitmap(new Uri(cachedPath), version);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteImage] 加载失败 '{source}': {ex.Message}");
            if (version == _loadVersion)
            {
                ShowPlaceholder();
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                _loadCts = null;
            }
        }
    }

    private void SetBitmap(Uri uri, int version)
    {
        if (version != _loadVersion)
        {
            return;
        }

        try
        {
            PlaceholderBorder.Visibility = Visibility.Visible;
            ContentImage.Source = new BitmapImage(uri);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[RemoteImage] 创建 BitmapImage 失败 '{uri}': {ex.Message}");
            ShowPlaceholder();
        }
    }

    private void OnImageOpened(object sender, RoutedEventArgs e)
    {
        PlaceholderBorder.Visibility = Visibility.Collapsed;
    }

    private void OnImageFailed(object sender, ExceptionRoutedEventArgs e)
    {
        Debug.WriteLine($"[RemoteImage] 图片解码失败: {e.ErrorMessage}");
        ShowPlaceholder();


        Uri? remoteUri = _currentRemoteUri;
        _currentRemoteUri = null;
        if (remoteUri is not null)
        {
            RemoteImageCache.Invalidate(remoteUri);
        }
    }

    private void ShowPlaceholder()
    {
        PlaceholderBorder.Visibility = Visibility.Visible;
        ContentImage.Source = null;
    }

    private void CancelPending()
    {
        CancellationTokenSource? cts = _loadCts;
        _loadCts = null;

        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePlaceholderGlyphSize();
    }


    private void UpdatePlaceholderGlyphSize()
    {
        double edge = Math.Min(ActualWidth, ActualHeight);
        if (double.IsNaN(edge) || double.IsInfinity(edge) || edge <= 0)
        {
            edge = 24;
        }

        PlaceholderGlyph.FontSize = Math.Clamp(edge * 0.4, 10, 32);
    }

    #region URI 判定

    private static bool TryBuildLocalUri(string source, out Uri? uri)
    {
        uri = null;

        if (!source.Contains("://", StringComparison.Ordinal))
        {
            if (source.Length >= 3 && source[1] == ':' && (source[2] == '\\' || source[2] == '/'))
            {
                return Uri.TryCreate(source, UriKind.Absolute, out uri);
            }


            if (source.Length > 2 && source[0] == '\\' && source[1] == '\\')
            {
                return Uri.TryCreate(source, UriKind.Absolute, out uri);
            }


            return Uri.TryCreate("ms-appx:///" + source.TrimStart('/'), UriKind.Absolute, out uri);
        }

        int schemeEnd = source.IndexOf("://", StringComparison.Ordinal);
        string scheme = source[..schemeEnd];

        if (scheme.Equals("ms-appx", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("ms-appx-web", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("ms-appdata", StringComparison.OrdinalIgnoreCase)
            || scheme.Equals("file", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.TryCreate(source, UriKind.Absolute, out uri);
        }

        return false;
    }

    private static bool IsHttpUri(Uri uri)
    {
        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}