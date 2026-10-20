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

/// <summary>
/// 周期挑战通用图片控件。
/// <list type="bullet">
/// <item><c>ms-appx://</c> / <c>file://</c> / 本地绝对路径 / 相对资源路径：直接交给 <see cref="BitmapImage"/>。</item>
/// <item><c>http(s)</c>：先经 <see cref="RemoteImageCache"/> 下载到本地磁盘缓存，再加载本地文件。</item>
/// <item>加载中与加载失败都显示占位块（不再抛出异常）；成功后通过
/// <c>Helpers/ImageFadeInHelper</c> 附加属性淡入。</item>
/// <item>控件 <c>Unloaded</c> 时取消等待（共享下载不会被中断，其它控件仍可命中缓存），
/// 重新 <c>Loaded</c> 且 Source 未变化时不会重复加载。</item>
/// </list>
/// </summary>
public sealed partial class RemoteImage : UserControl
{
    /// <summary>图片来源：支持 ms-appx / file / 本地绝对路径 / 相对资源路径 / http(s)。</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source),
        typeof(string),
        typeof(RemoteImage),
        new PropertyMetadata(string.Empty, OnSourceChanged));

    /// <summary>图片来源（字符串）。空字符串表示无图，只显示占位。</summary>
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

        // 从可视树移除后再挂载：图片还在且 Source 未变化就不需要重新加载
        if (ContentImage.Source is not null && string.Equals(_displayedSource, current, StringComparison.Ordinal))
        {
            return;
        }

        Reload(Source);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // 取消本次等待；共享下载任务会继续跑完以便命中磁盘缓存
        CancelPending();
        _loadVersion++;
    }

    /// <summary>按新的 Source 重新加载（版本号保证旧请求的返回被丢弃）。</summary>
    private void Reload(string? source)
    {
        CancelPending();
        int version = ++_loadVersion;

        string trimmed = source?.Trim() ?? string.Empty;
        _displayedSource = trimmed;
        _currentRemoteUri = null;

        // 尚未挂到可视树时只显示占位，等 Loaded 再加载
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
            // 本地资源 / 本地文件：直接交给 BitmapImage
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
            // 先显示占位，等 ImageOpened 再隐藏；淡入由 ImageFadeInHelper 负责
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

        // 磁盘缓存可能损坏，删掉后下次重新下载
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
            // 已释放，忽略
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdatePlaceholderGlyphSize();
    }

    /// <summary>占位字形随控件尺寸缩放，避免小图标里字形溢出。</summary>
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

    /// <summary>
    /// 判断是否为本地资源：ms-appx / ms-appdata / file / Windows 绝对路径 / 相对资源路径。
    /// </summary>
    private static bool TryBuildLocalUri(string source, out Uri? uri)
    {
        uri = null;

        if (!source.Contains("://", StringComparison.Ordinal))
        {
            // Windows 绝对路径：C:\foo\bar.png 或 C:/foo/bar.png
            if (source.Length >= 3 && source[1] == ':' && (source[2] == '\\' || source[2] == '/'))
            {
                return Uri.TryCreate(source, UriKind.Absolute, out uri);
            }

            // UNC 路径：\\server\share\x.png
            if (source.Length > 2 && source[0] == '\\' && source[1] == '\\')
            {
                return Uri.TryCreate(source, UriKind.Absolute, out uri);
            }

            // 其余视为应用资源相对路径（ms-appx:///Assets/...）
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
