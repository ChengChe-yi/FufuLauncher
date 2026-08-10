/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models.MiHoYo.Identity;
using FufuLauncher.Services.MiHoYo.Networking;
using FufuLauncher.Services.MiHoYo.Transport;

namespace FufuLauncher.Services.MiHoYo;

public sealed class DailyNoteService
{
    private readonly AccountIdentityService _identityService;

    private const string Page = "v6.6.1-gr-cn_#/ys";
    private const string DailyNoteUrl = "https://api-takumi-record.mihoyo.com/game_record/app/genshin/api/dailyNote";
    private const string WidgetUrl = "https://api-takumi-record.mihoyo.com/game_record/app/genshin/aapi/widget/v2?game_id=2";

    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public DailyNoteService()
    {
        _identityService = App.GetService<AccountIdentityService>()
            ?? throw new InvalidOperationException("DailyNote_NoIdentityService".GetLocalized());
    }

    public async Task<DailyNoteCardData> GetDailyNoteAsync(string roleId, string server)
    {
        await _semaphore.WaitAsync();
        try
        {
            AccountManager accountManager = App.GetService<AccountManager>();
            string activeId = accountManager.ActiveAccountId ?? throw new InvalidOperationException("DailyNote_NoActiveAccount".GetLocalized());

            // 身份从聚合入口取：cookies + 设备指纹 + UA 同源
            var ctx = await _identityService.BuildAsync(activeId);
            if (ctx.Cookies.Count == 0)
                throw new InvalidOperationException("DailyNote_CannotLoadCookie".GetLocalized());

            string apiUrl = $"{DailyNoteUrl}?server={Uri.EscapeDataString(server)}&role_id={Uri.EscapeDataString(roleId)}";
            string json = await RequestDailyNoteAsync(apiUrl, ctx, null);
            var (retcode, message) = ParseResponse(json);

            if (retcode == 1034)
            {
                var localSettingsService = App.GetService<ILocalSettingsService>();
                var captchaDisabledJson = await localSettingsService.ReadSettingAsync("IsCaptchaPopupDisabled");
                bool isCaptchaDisabled = captchaDisabledJson != null && Convert.ToBoolean(captchaDisabledJson);

                if (!isCaptchaDisabled)
                {
                    GeetestService geetestService = new();
                    string xrpcChallenge = await geetestService.TryVerifyForDailyNoteAsync(ctx);
                    if (!string.IsNullOrEmpty(xrpcChallenge))
                    {
                        json = await RequestDailyNoteAsync(apiUrl, ctx, xrpcChallenge);
                        (retcode, message) = ParseResponse(json);
                    }
                }
                else
                {
                    Debug.WriteLine("[DailyNoteService] 风控验证码弹窗已被用户禁用，跳过验证");
                }
            }

            if (retcode == 5003 || retcode == 1034)
            {
                json = await RequestWidgetAsync(ctx);
                (retcode, message) = ParseResponse(json);
            }

            if (retcode != 0)
                throw new InvalidOperationException(string.Format("DailyNote_FetchFailed".GetLocalized(), message, retcode));

            return DailyNoteParser.Parse(json);
        }
        finally { _semaphore.Release(); }
    }

    private async Task<string> RequestDailyNoteAsync(string apiUrl, AccountContext ctx, string? xrpcChallenge)
    {
        string cookieStr = BbsRequestBuilder.BuildCookieString(ctx.Cookies, BbsRequestBuilder.CookieMode.Cookie);
        string query = new Uri(apiUrl).Query.TrimStart('?');
        string sortedQuery = string.Join("&", query.Split('&').OrderBy(s => s, StringComparer.Ordinal));

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
            SortedQuery: sortedQuery,
            Challenge: xrpcChallenge,
            ToolVersion: HeaderVersions.ToolVersionCn,
            Page: Page));
        req.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7");

        using var resp = await _httpClient.SendAsync(req);
        return await resp.Content.ReadAsStringAsync();
    }

    private async Task<string> RequestWidgetAsync(AccountContext ctx)
    {
        string cookieStr = BbsRequestBuilder.BuildCookieString(ctx.Cookies, BbsRequestBuilder.CookieMode.SToken);
        string sortedQuery = string.Join("&", WidgetUrl.Split('?', 2)[1].Split('&').OrderBy(s => s, StringComparer.Ordinal));

        using var req = new HttpRequestMessage(HttpMethod.Get, WidgetUrl);
        MiHoYoHeaderFactory.ApplyGameRecordHeaders(req, new GameRecordHeaderOptions(
            AppVersion: HeaderVersions.MobileCnLogin,
            UserAgent: ctx.UserAgent.Mobile,
            DeviceId: ctx.Device.BbsDeviceId,
            DeviceFp: ctx.Device.DeviceFp,
            DeviceName: Uri.EscapeDataString(ctx.Device.DeviceName),
            SysVersion: ctx.Device.SysVersion,
            Cookie: cookieStr,
            DsSalt: HeaderSalts.CnX6,
            SortedQuery: sortedQuery,
            Page: Page));
        req.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7");

        using var resp = await _httpClient.SendAsync(req);
        return await resp.Content.ReadAsStringAsync();
    }

    internal static string CalculateDS2(string salt, string query, string body) =>
        MiHoYoHeaderFactory.CalculateDs2(salt, query, body);

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
            // 非 JSON 响应（风控 HTML / 网关空 body）：返回哨兵 retcode，走本地化错误路径
            return (-1, "Status_UnknownError".GetLocalized());
        }
    }
}
