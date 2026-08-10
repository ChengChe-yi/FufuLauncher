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

/// <summary>
/// <see cref="IBbsRequestBuilder"/> 默认实现：按场景从 <see cref="AccountContext"/> 组装请求头。
/// <para>已接入场景：DailyNote / DailyNoteWidget / Geetest / GetFpNative；BBS 社区系（UserFullInfo / CommunitySign）与登录系（WebLogin）待迁移后实现。</para>
/// </summary>
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
                dsSalt: HeaderSalts.CnX4, cookieMode: CookieMode.Cookie, acceptLanguage: true, toolVersion: HeaderVersions.ToolVersionCn, page: Page),
            BbsRequestScene.DailyNoteWidget => BuildGameRecord(ctx, method, url, body, challenge, options,
                dsSalt: HeaderSalts.CnX6, cookieMode: CookieMode.SToken, acceptLanguage: true),
            BbsRequestScene.Geetest => BuildGameRecord(ctx, method, url, body, challenge, options,
                dsSalt: HeaderSalts.CnX4, cookieMode: CookieMode.Cookie, acceptLanguage: false, defaultChallengeGame: "2"),
            BbsRequestScene.GetFpNative => BuildGetFp(ctx, method, url, body),

            BbsRequestScene.UserFullInfo or BbsRequestScene.CommunitySign or BbsRequestScene.WebLogin =>
                throw new NotSupportedException($"BbsRequestScene.{scene} 尚未接入场景化 Builder（BBS 社区系 / 登录系迁移后实现）"),

            _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, null)
        };
    }

    /// <summary>
    /// game_record 系（client_type=5，X4/X6 + DS2，WebView 头）。
    /// </summary>
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

    /// <summary>getFp 原生通道：只有 okhttp UA，无 x-rpc 系列头、无 DS。</summary>
    private static HttpRequestMessage BuildGetFp(AccountContext ctx, HttpMethod method, string url, string? body)
    {
        var req = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(body))
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        MiHoYoHeaderFactory.ApplyDeviceFpHeaders(req, ctx.UserAgent.OkHttp);
        return req;
    }

    /// <summary>解析 challenge_game：优先 options 值，缺省用场景默认；非数字返回 null（不抛异常）。</summary>
    private static int? ResolveChallengeGame(string? value, string? fallback)
    {
        string? candidate = !string.IsNullOrEmpty(value) ? value : fallback;
        return candidate is not null && int.TryParse(candidate, out var v) ? v : null;
    }

    /// <summary>
    /// cookie 拼接（单一实现；各服务统一走此方法）。
    /// <para>Full = CookieToken | LToken：v1 键优先，缺 v1 时逐键独立回退 v2（保留各自键名）；</para>
    /// <para>SToken：stoken/mid/stuid 成列表拼接，避免缺 stoken 时以分号开头产生畸形 Cookie 头。</para>
    /// </summary>
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

    /// <summary>按顺序取第一个非空值（不关心键名）。</summary>
    private static string FirstNonEmpty(IReadOnlyDictionary<string, string> cookies, params string[] keys)
    {
        foreach (var key in keys)
            if (cookies.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return v;
        return "";
    }

    /// <summary>按顺序取第一个非空键值对（保留原始键名，用于 v1/v2 逐键回退）。</summary>
    private static (string Key, string Value)? FirstPair(IReadOnlyDictionary<string, string> cookies, params string[] keys)
    {
        foreach (var key in keys)
            if (cookies.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
                return (key, v);
        return null;
    }

    /// <summary>cookie 模式：Full = CookieToken | LToken；SToken = stoken 系。</summary>
    internal enum CookieMode
    {
        Cookie, SToken
    }
}
