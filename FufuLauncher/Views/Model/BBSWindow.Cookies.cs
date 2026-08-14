/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Services;

namespace FufuLauncher.Views;

public sealed partial class BBSWindow
{
    #region 设备指纹与账号 Cookie

    private Dictionary<string, string> cookieDic = new();

    private string _activeDeviceFp = string.Empty;

    private async Task EnsureDeviceFpAsync()
    {
        try
        {
            var accountManager = App.GetService<AccountManager>();
            var activeId = accountManager.ActiveAccountId;
            if (string.IsNullOrEmpty(activeId))
            {
                System.Diagnostics.Debug.WriteLine("[BBSWindow] 无活跃账号，跳过指纹获取");
                return;
            }

            var cookies = await accountManager.LoadCookiesAsync(activeId);
            if (cookies == null || cookies.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[BBSWindow] 无可用 Cookies，跳过指纹获取");
                return;
            }

            _activeDeviceFp = await _fingerprintService.GetOrRegisterFingerprintAsync(activeId, cookies);
            System.Diagnostics.Debug.WriteLine($"[BBSWindow] 活跃账号指纹已获取: {_activeDeviceFp}");

            if (!string.IsNullOrEmpty(_activeDeviceFp))
            {
                cookieDic["DEVICEFP"] = _activeDeviceFp;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BBSWindow] EnsureDeviceFpAsync 失败: {ex.Message}");
        }
    }

    private async Task LoadPageAsync(string url)
    {
        System.Diagnostics.Debug.WriteLine($"[BBSWindow] LoadPageAsync called with URL: {url}");
        await LoadActiveAccountCookiesAsync();

        var manager = BBSWebView.CoreWebView2.CookieManager;
        if (BBSWebView.Source == null || BBSWebView.Source.ToString() == "about:blank")
        {
            var cookies = await manager.GetCookiesAsync("https://webstatic.mihoyo.com");
            foreach (var c in cookies) manager.DeleteCookie(c);
        }

        foreach (var kv in cookieDic)
        {
            var cookie = manager.CreateCookie(kv.Key, kv.Value, ".mihoyo.com", "/");
            manager.AddOrUpdateCookie(cookie);
        }
        System.Diagnostics.Debug.WriteLine($"[BBSWindow] Added {cookieDic.Count} cookies to WebView2");
        BBSWebView.CoreWebView2.Navigate(url);
    }

    private async Task LoadActiveAccountCookiesAsync()
    {
        cookieDic.Clear();

        var accountManager = App.GetService<AccountManager>();
        var activeId = accountManager.ActiveAccountId;
        if (activeId == null) return;

        var cookies = await accountManager.LoadCookiesAsync(activeId);
        if (cookies == null || cookies.Count == 0) return;

        foreach (var kv in cookies)
        {
            if (!string.IsNullOrWhiteSpace(kv.Key) && !string.IsNullOrWhiteSpace(kv.Value))
            {
                cookieDic[kv.Key] = kv.Value;
            }
        }
    }

    private void ParseCookie(string cookieStr)
    {
        cookieDic.Clear();
        if (string.IsNullOrWhiteSpace(cookieStr)) return;
        foreach (var item in cookieStr.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = item.Split('=', 2);
            if (kv.Length == 2) cookieDic[kv[0].Trim()] = kv[1].Trim();
        }
    }

    #endregion
}
