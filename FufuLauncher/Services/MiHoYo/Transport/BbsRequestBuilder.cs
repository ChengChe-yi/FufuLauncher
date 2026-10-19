/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Net.Http;
using System.Text;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Models.MiHoYo.Identity;
using FufuLauncher.Services.MiHoYo.Networking;

namespace FufuLauncher.Services.MiHoYo.Transport;

public sealed class BbsRequestBuilder : IBbsRequestBuilder
{
    private const string Page = "v6.6.1-gr-cn_#/ys";

    public HttpRequestMessage Build(
        AccountContext ctx,
        BbsRequestScene scene,
        HttpMethod method,
        string url,
        string? body = null,
        string? challenge = null,
        BbsRequestOptions? options = null)
    {
        return scene switch
        {
            BbsRequestScene.DailyNote => BuildGameRecord(ctx, method, url, body, challenge, options,
                dsSalt: HeaderSalts.CnX4, cookieMode: CookieMode.Cookie, acceptLanguage: true,
                toolVersion: HeaderVersions.ToolVersionCn, page: Page),
            BbsRequestScene.DailyNoteWidget => BuildGameRecord(ctx, method, url, body, challenge, options,
                dsSalt: HeaderSalts.CnX6, cookieMode: CookieMode.SToken, acceptLanguage: true),
            BbsRequestScene.Geetest => BuildGameRecord(ctx, method, url, body, challenge, options,
                dsSalt: HeaderSalts.CnX4, cookieMode: CookieMode.Cookie, acceptLanguage: false,
                defaultChallengeGame: "2"),
            BbsRequestScene.GetFpNative => BuildGetFp(ctx, method, url, body),

            BbsRequestScene.UserFullInfo or BbsRequestScene.CommunitySign or BbsRequestScene.WebLogin =>
                throw new NotSupportedException($"BbsRequestScene.{scene} 尚未接入场景化 Builder（BBS 社区系 / 登录系迁移后实现）"),

            _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null)
        };
    }


    private static HttpRequestMessage BuildGameRecord(
        AccountContext ctx,
        HttpMethod method,
        string url,
        string? body,
        string? challenge,
        BbsRequestOptions? options,
        string dsSalt,
        CookieMode cookieMode,
        bool acceptLanguage,
        string? toolVersion = null,
        string? page = null,
        string? defaultChallengeGame = null)
    {
        string cookieStr = BuildCookieString(ctx.Cookies, cookieMode);
        string query = new Uri(url).Query.TrimStart('?');
        string sortedQuery = string.Join("&", query.Split('&').OrderBy(s => s, StringComparer.Ordinal));

        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(body))
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

        MiHoYoHeaderFactory.ApplyGameRecordHeaders(req, new GameRecordHeaderOptions(
            AppVersion: HeaderVersions.MobileCnLogin,
            UserAgent: ctx.UserAgent.Mobile,
            DeviceId: ctx.Device.BbsDeviceId,
            DeviceFp: ctx.Device.DeviceFp,
            DeviceName: Uri.EscapeDataString(ctx.Device.DeviceName),
            SysVersion: ctx.Device.SysVersion,
            Cookie: cookieStr,
            DsSalt: dsSalt,
            SortedQuery: sortedQuery,
            Body: body ?? "",
            Challenge: challenge,
            ChallengeGame: ResolveChallengeGame(options?.ChallengeGame, defaultChallengeGame),
            ChallengePath: options?.ChallengePath,
            ToolVersion: toolVersion,
            Page: page));

        if (acceptLanguage)
            req.Headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en-US;q=0.8,en;q=0.7");

        return req;
    }


    private static HttpRequestMessage BuildGetFp(AccountContext ctx, HttpMethod method, string url, string? body)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(body))
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        MiHoYoHeaderFactory.ApplyDeviceFpHeaders(req, ctx.UserAgent.OkHttp);
        return req;
    }


    private static int? ResolveChallengeGame(string? value, string? fallback)
    {
        string? candidate = !string.IsNullOrEmpty(value) ? value : fallback;
        return candidate is not null && int.TryParse(candidate, out var v) ? v : null;
    }


    internal static string BuildCookieString(IReadOnlyDictionary<string, string> cookies, CookieMode mode)
    {
        if (mode == CookieMode.SToken)
        {
            var pairs = new List<string>();
            if (cookies.TryGetValue("stoken", out var stoken) && !string.IsNullOrEmpty(stoken))
                pairs.Add($"stoken={stoken}");
            if (cookies.TryGetValue("mid", out var mid) && !string.IsNullOrEmpty(mid))
                pairs.Add($"mid={mid}");
            string stuid = FirstNonEmpty(cookies, "stuid", "account_id", "ltuid_v2");
            if (!string.IsNullOrEmpty(stuid))
                pairs.Add($"stuid={stuid}");
            return string.Join(";", pairs);
        }

        var full = new List<string>();
        if (FirstPair(cookies, "account_id", "account_id_v2") is { } aid)
            full.Add($"{aid.Key}={aid.Value}");
        if (FirstPair(cookies, "cookie_token", "cookie_token_v2") is { } ct)
            full.Add($"{ct.Key}={ct.Value}");
        var lt = FirstPair(cookies, "ltoken", "ltoken_v2");
        var lu = FirstPair(cookies, "ltuid", "ltuid_v2");
        if (lt is { } ltp && lu is { } lup)
        {
            full.Add($"{ltp.Key}={ltp.Value}");
            full.Add($"{lup.Key}={lup.Value}");
        }

        return string.Join(";", full);
    }


    private static string FirstNonEmpty(IReadOnlyDictionary<string, string> cookies, params string[] keys)
    {
        foreach (var key in keys)
            if (cookies.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
        return "";
    }


    private static (string Key, string Value)? FirstPair(IReadOnlyDictionary<string, string> cookies,
        params string[] keys)
    {
        foreach (var key in keys)
            if (cookies.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return (key, v);
        return null;
    }


    internal enum CookieMode
    {
        Cookie,
        SToken
    }
}