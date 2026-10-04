/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text.Json;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models.MiHoYo;
using FufuLauncher.Models.MiHoYo.Identity;
using FufuLauncher.Services.MiHoYo.Networking;
using FufuLauncher.Services.MiHoYo.Transport;

namespace FufuLauncher.Services.MiHoYo.DailyNote;

public sealed class DailyNoteService
{
    private const string RecordApiBase = "https://api-takumi-record.mihoyo.com/game_record/app/genshin";

    private const string IndexPath = "/api/index";
    private const string DailyNotePath = "/api/dailyNote";
    private const string WidgetPath = "/aapi/widget/v2?game_id=2";

    private const string Page = "v6.6.1-gr-cn_#/ys";

    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly AccountIdentityService _identityService;
    private readonly OverseaGameRecordClient _overseaGameRecordClient = new();

    public DailyNoteService()
    {
        _identityService = App.GetService<AccountIdentityService>()
            ?? throw new InvalidOperationException("DailyNote_NoIdentityService".GetLocalized());
    }

    public async Task<DailyNoteCardData?> GetDailyNoteAsync(string roleId, string server)
    {
        await _semaphore.WaitAsync();
        try
        {
            AccountManager accountManager = App.GetService<AccountManager>();
            string activeId = accountManager.ActiveAccountId
                ?? throw new InvalidOperationException("DailyNote_NoActiveAccount".GetLocalized());

            var ctx = await _identityService.BuildAsync(activeId);
            if (ctx.Cookies.Count == 0)
                throw new InvalidOperationException("DailyNote_CannotLoadCookie".GetLocalized());

            if (ServerRegion.IsOversea(server))
            {
                return await _overseaGameRecordClient.GetDailyNoteAsync(roleId, server, ctx.Cookies);
            }

            string playerQuery = $"role_id={Uri.EscapeDataString(roleId)}&server={Uri.EscapeDataString(server)}";

            // 先在 index 预热一次（真实客户端顺序），再取便签。
            await WarmUpPlayerInfoAsync(playerQuery, ctx);

            string json = await RequestAsync(
                $"{RecordApiBase}{DailyNotePath}?{playerQuery}", ctx, challenge: null);
            var (retcode, message) = ParseResponse(json);

            if (retcode == 10001)
            {
                json = await RetryWithRefreshedCookiesAsync(accountManager, activeId, playerQuery, ctx);
                if (json is null) return null;
                (retcode, message) = ParseResponse(json);
            }

            if (retcode == 1034)
            {
                string challenged = await TrySolveChallengeAsync(playerQuery, ctx, json);
                var (challengeRetcode, challengeMessage) = ParseResponse(challenged);

                if (challengeRetcode == 0)
                {
                    json = challenged;
                    (retcode, message) = (challengeRetcode, challengeMessage);
                }
                else
                {
                    // 验证后重试仍未通过：保持 1034，交由下面的 widget 兜底通道再试。
                    Debug.WriteLine(
                        $"[DailyNoteService] 验证后重试 retcode={challengeRetcode}，转 widget 兜底");
                }
            }

            if (retcode == 5003 || retcode == 1034)
            {
                json = await RequestWidgetAsync(ctx);
                (retcode, message) = ParseResponse(json);
            }

            if (retcode != 0)
                throw new InvalidOperationException(
                    string.Format("DailyNote_FetchFailed".GetLocalized(), message, retcode));

            return DailyNoteParser.Parse(json);
        }
        finally { _semaphore.Release(); }
    }

    private async Task WarmUpPlayerInfoAsync(string playerQuery, AccountContext ctx)
    {
        try
        {
            string query = $"avatar_list_type=1&{playerQuery}";
            await RequestAsync($"{RecordApiBase}{IndexPath}?{query}", ctx, challenge: null);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DailyNoteService] index 预热失败（忽略）: {ex.Message}");
        }
    }

    private async Task<string?> RetryWithRefreshedCookiesAsync(
        AccountManager accountManager, string activeId, string playerQuery, AccountContext ctx)
    {
        Debug.WriteLine("[DailyNoteService] retcode10001");

        var currentCookies = await accountManager.LoadCookiesAsync(activeId);
        if (currentCookies is null || currentCookies.Count == 0)
        {
            Debug.WriteLine("[DailyNoteService] 无法加载当前Cookie");
            return null;
        }

        var refreshed = await new TokenRefreshService().RefreshCookieAsync(currentCookies);
        if (refreshed is null || refreshed.Count == 0)
        {
            Debug.WriteLine("[DailyNoteService] Cookie刷新失败");
            return null;
        }

        await accountManager.UpdateCookiesAsync(activeId, refreshed);
        var rebuilt = await _identityService.BuildAsync(activeId);

        string json = await RequestAsync(
            $"{RecordApiBase}{DailyNotePath}?{playerQuery}", rebuilt, challenge: null);
        var (retcode, _) = ParseResponse(json);
        Debug.WriteLine($"[DailyNoteService] Cookie刷新后重试retcode={retcode}");

        return retcode == 10001 ? null : json;
    }

    private async Task<string> TrySolveChallengeAsync(string playerQuery, AccountContext ctx, string fallback)
    {
        var localSettingsService = App.GetService<ILocalSettingsService>();
        var captchaDisabledJson = await localSettingsService.ReadSettingAsync("IsCaptchaPopupDisabled");
        if (captchaDisabledJson != null && Convert.ToBoolean(captchaDisabledJson))
        {
            Debug.WriteLine("[DailyNoteService] 风控验证码弹窗已被用户禁用，跳过验证");
            return fallback;
        }

        string xrpcChallenge = await new GeetestService().TryVerifyForDailyNoteAsync(ctx);
        if (string.IsNullOrEmpty(xrpcChallenge))
        {
            return fallback;
        }

        return await RequestAsync(
            $"{RecordApiBase}{DailyNotePath}?{playerQuery}", ctx, xrpcChallenge);
    }

    private async Task<string> RequestWidgetAsync(AccountContext ctx)
    {
        string url = RecordApiBase + WidgetPath;
        string cookieStr = BbsRequestBuilder.BuildCookieString(ctx.Cookies, BbsRequestBuilder.CookieMode.SToken);

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        MiHoYoHeaderFactory.ApplyGameRecordHeaders(req, new GameRecordHeaderOptions(
            AppVersion: HeaderVersions.MobileCnLogin,
            UserAgent: ctx.UserAgent.Mobile,
            DeviceId: ctx.Device.BbsDeviceId,
            DeviceFp: ctx.Device.DeviceFp,
            DeviceName: Uri.EscapeDataString(ctx.Device.DeviceName),
            SysVersion: ctx.Device.SysVersion,
            Cookie: cookieStr,
            DsSalt: HeaderSalts.CnX6,
            SortedQuery: SortedQuery(url),
            Page: Page));
        req.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7");

        using var resp = await _httpClient.SendAsync(req);
        return await resp.Content.ReadAsStringAsync();
    }

    private async Task<string> RequestAsync(string apiUrl, AccountContext ctx, string? challenge)
    {
        string cookieStr = BbsRequestBuilder.BuildCookieString(ctx.Cookies, BbsRequestBuilder.CookieMode.Cookie);

        using var req = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        MiHoYoHeaderFactory.ApplyGameRecordHeaders(req, new GameRecordHeaderOptions(
            AppVersion: HeaderVersions.MobileCnLogin,
            UserAgent: ctx.UserAgent.Mobile,
            DeviceId: ctx.Device.BbsDeviceId,
            DeviceFp: ctx.Device.DeviceFp,
            DeviceName: Uri.EscapeDataString(ctx.Device.DeviceName),
            SysVersion: ctx.Device.SysVersion,
            Cookie: cookieStr,
            DsSalt: HeaderSalts.CnX4,
            SortedQuery: SortedQuery(apiUrl),
            Challenge: challenge,
            ToolVersion: HeaderVersions.ToolVersionCn,
            Page: Page));
        req.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7");

        using var resp = await _httpClient.SendAsync(req);
        return await resp.Content.ReadAsStringAsync();
    }

    private static string SortedQuery(string url) =>
        string.Join("&", new Uri(url).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(s => s, StringComparer.Ordinal));

    private static (int Retcode, string Message) ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            int retcode = root.TryGetProperty("retcode", out var rc) ? rc.GetInt32() : -1;
            string message = root.TryGetProperty("message", out var m)
                ? m.GetString() ?? "Status_UnknownError".GetLocalized()
                : "Status_UnknownError".GetLocalized();
            return (retcode, message);
        }
        catch (JsonException)
        {
            return (-1, "Status_UnknownError".GetLocalized());
        }
    }
}
