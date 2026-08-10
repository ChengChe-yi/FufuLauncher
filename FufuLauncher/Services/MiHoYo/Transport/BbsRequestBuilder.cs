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
                dsSalt: HeaderSalts.CnX4, cookieMode: CookieMode.Full, acceptLanguage: true, toolVersion: HeaderVersions.ToolVersionCn, page: Page),
            BbsRequestScene.DailyNoteWidget => BuildGameRecord(ctx, method, url, body, challenge, options,
                dsSalt: HeaderSalts.CnX6, cookieMode: CookieMode.SToken, acceptLanguage: true),
            BbsRequestScene.Geetest => BuildGameRecord(ctx, method, url, body, challenge, options,
                dsSalt: HeaderSalts.CnX4, cookieMode: CookieMode.Full, acceptLanguage: false),
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
        string? page = null)
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
            ChallengeGame: options?.ChallengeGame is { } cg ? int.Parse(cg) : null,
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

    /// <summary>
    /// cookie 拼接。与 DailyNoteService.BuildCookieString 逻辑同步；各服务迁入 Builder 后收敛于此。
    /// </summary>
    internal static string BuildCookieString(IReadOnlyDictionary<string, string> cookies, CookieMode mode)
    {
        var sb = new StringBuilder();
        if (mode == CookieMode.SToken)
        {
            if (cookies.TryGetValue("stoken", out var stoken) && !string.IsNullOrEmpty(stoken)) sb.Append($"stoken={stoken}");
            if (cookies.TryGetValue("mid", out var mid) && !string.IsNullOrEmpty(mid)) sb.Append($";mid={mid}");
            string stuid = cookies.GetValueOrDefault("stuid") ?? cookies.GetValueOrDefault("account_id") ?? cookies.GetValueOrDefault("ltuid_v2") ?? "";
            if (!string.IsNullOrEmpty(stuid)) sb.Append($";stuid={stuid}");
        }
        else
        {
            // Full = CookieToken | LToken：account_id + cookie_token + ltoken + ltuid（后两者全有才追加）
            // v1 键优先，缺 v1 时整体回退 v2 键
            string tokenSuffix = cookies.ContainsKey("cookie_token") ? string.Empty : "_v2";

            string aid = cookies.GetValueOrDefault($"account_id{tokenSuffix}");
            if (!string.IsNullOrEmpty(aid))
            {
                sb.Append($"account_id{tokenSuffix}={aid}");
            }

            string cookieToken = cookies.GetValueOrDefault($"cookie_token{tokenSuffix}");
            if (!string.IsNullOrEmpty(cookieToken))
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append($"cookie_token{tokenSuffix}={cookieToken}");
            }

            string ltoken = cookies.GetValueOrDefault($"ltoken{tokenSuffix}");
            string ltuid = cookies.GetValueOrDefault($"ltuid{tokenSuffix}");
            if (!string.IsNullOrEmpty(ltoken) && !string.IsNullOrEmpty(ltuid))
            {
                if (sb.Length > 0) sb.Append(';');
                sb.Append($"ltoken{tokenSuffix}={ltoken};ltuid{tokenSuffix}={ltuid}");
            }
        }
        return sb.ToString();
    }

    /// <summary>cookie 模式，与 DailyNoteService.CookieMode 语义一致；替换阶段收敛。</summary>
    internal enum CookieMode
    {
        Full, SToken
    }
}
