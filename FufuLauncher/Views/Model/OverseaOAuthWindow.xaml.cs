/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Models.MiHoYo.Passport;
using FufuLauncher.Services.MiHoYo.Passport;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;
using Windows.Graphics;

namespace FufuLauncher.Views;

public sealed partial class OverseaOAuthWindow : Window
{
    private const int CallbackPrefixLength = 18;

    private readonly OverseaThirdPartyKind _kind;
    private readonly TaskCompletionSource<ThirdPartyToken?> _resultTcs = new();
    private ThirdPartyToken? _result;

    public OverseaOAuthWindow(OverseaThirdPartyKind kind)
    {
        _kind = kind;
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.Resize(new SizeInt32(480, 700));

        Closed += OverseaOAuthWindow_Closed;
        if (Content is FrameworkElement rootContent)
        {
            rootContent.Loaded += RootContent_Loaded;
        }
    }
    
    public Task<ThirdPartyToken?> ShowAndWaitAsync()
    {
        Activate();
        return _resultTcs.Task;
    }

    private async void RootContent_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement rootContent)
        {
            rootContent.Loaded -= RootContent_Loaded;
        }

        await StartOAuthAsync();
    }

    private async Task StartOAuthAsync()
    {
        try
        {
            await OAuthWebView.EnsureCoreWebView2Async();
            var settings = OAuthWebView.CoreWebView2.Settings;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
            
            var cookieManager = OAuthWebView.CoreWebView2.CookieManager;
            var cookies = await cookieManager.GetCookiesAsync("https://account.hoyoverse.com");
            foreach (var cookie in cookies)
            {
                cookieManager.DeleteCookie(cookie);
            }

            OAuthWebView.CoreWebView2.NavigationStarting -= OnNavigationStarting;
            OAuthWebView.CoreWebView2.NavigationStarting += OnNavigationStarting;

            string languageCode = (FufuLauncher.Helpers.ResourceExtensions.CurrentCulture ?? "zh-cn").ToLowerInvariant();
            OAuthWebView.CoreWebView2.Navigate(OverseaThirdPartyOAuth.BuildLoginUrl(_kind, languageCode));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OverseaOAuthWindow] OAuth 初始化失败: {ex.Message}");
            _resultTcs.TrySetResult(null);
            DispatcherQueue.TryEnqueue(Close);
        }
    }

    private void OnNavigationStarting(CoreWebView2 sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (e.Uri.StartsWith("about:blank", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            
            ReadOnlySpan<char> uriSpan = e.Uri.AsSpan()[CallbackPrefixLength..];
            int ampIndex = uriSpan.IndexOf('&');
            ReadOnlySpan<char> tokenSpan = ampIndex >= 0 ? uriSpan[..ampIndex] : uriSpan;
            _result = new ThirdPartyToken(OverseaThirdPartyOAuth.GetTypeCode(_kind), Uri.UnescapeDataString(tokenSpan.ToString()));

            _resultTcs.TrySetResult(_result);
            DispatcherQueue.TryEnqueue(Close);
        }
    }

    private void OverseaOAuthWindow_Closed(object sender, WindowEventArgs args)
    {
        _resultTcs.TrySetResult(_result);
        try
        {
            if (OAuthWebView.CoreWebView2 is not null)
            {
                OAuthWebView.CoreWebView2.NavigationStarting -= OnNavigationStarting;
                OAuthWebView.CoreWebView2.Stop();
                OAuthWebView.Close();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OverseaOAuthWindow] 关闭清理异常: {ex.Message}");
        }
    }
}
