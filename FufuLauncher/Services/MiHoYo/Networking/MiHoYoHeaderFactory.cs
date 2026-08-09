/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using FufuLauncher.Constants.MiHoYo;

namespace FufuLauncher.Services.MiHoYo.Networking;

/// <summary>
/// 米游社请求头统一构建工厂。
/// <para>
/// DS 规则：<c>x-rpc-client_type=5</c>（网页 / WebView 端）使用 X4 / X6 系 salt 配合 <b>DS2</b> 生成算法
/// （ds 形如 <c>t,r,md5</c>，salt 仅参与计算、不出现在 ds 中）；
/// <c>x-rpc-client_type=2/4</c>（App / 网页端）使用 X2 系 salt 配合 <b>DS1</b> 算法
/// （ds 形如 <c>t,r,salt,md5</c>，salt 明文携带）。
/// </para>
/// </summary>
public static class MiHoYoHeaderFactory
{
    /// <summary>DS1 生成（client_type=2/4）：<c>t,r,salt,md5</c>，salt 明文出现在 ds 中。</summary>
    public static string CalculateDs1(string salt, string query = "", string body = "")
    {
        long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int rand = Random.Shared.Next(100000, 200000);
        string r = (rand == 100000 ? 642367 : rand).ToString();
        string hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"salt={salt}&t={t}&r={r}&b={body}&q={query}"))).ToLowerInvariant();
        return $"{t},{r},{salt},{hash}";
    }

    /// <summary>DS2 生成（client_type=5，X4/X6 salt）：<c>t,r,md5</c>，salt 只参与计算不出现。</summary>
    public static string CalculateDs2(string salt, string query = "", string body = "")
    {
        long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int rand = Random.Shared.Next(100000, 200000);
        string r = (rand == 100000 ? 642367 : rand).ToString();
        string hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes($"salt={salt}&t={t}&r={r}&b={body}&q={query}"))).ToLowerInvariant();
        return $"{t},{r},{hash}";
    }

    /// <summary>按 client_type 选择 DS 算法：<see cref="ClientTypes.Other"/>(5) → DS2；其余 → DS1。</summary>
    public static string CalculateDs(string salt, string query, string body, string clientType)
        => clientType == ClientTypes.Other ? CalculateDs2(salt, query, body) : CalculateDs1(salt, query, body);

    /// <summary>
    /// 统一构建 game_record（api-takumi-record）系请求头，固定 <c>x-rpc-client_type=5</c>（WebView / 网页端）。
    /// DS 由 <see cref="CalculateDs2"/> 生成（X4 / X6 salt），query 需调用方传入已排序的 query 字符串。
    /// </summary>
    public static void ApplyGameRecordHeaders(HttpRequestMessage request, GameRecordHeaderOptions options)
    {
        request.Headers.Add(HeaderNames.Cookie, options.Cookie);
        request.Headers.Add(HeaderNames.RpcAppVersion, options.AppVersion);
        request.Headers.Add(HeaderNames.RpcClientType, ClientTypes.Other);
        request.Headers.Add(HeaderNames.RpcDeviceId, options.DeviceId);
        request.Headers.Add(HeaderNames.RpcDeviceName, options.DeviceName);
        request.Headers.Add(HeaderNames.RpcDeviceFp, options.DeviceFp);
        request.Headers.Add(HeaderNames.RpcSysVersion, options.SysVersion);
        if (!string.IsNullOrEmpty(options.ToolVersion)) request.Headers.Add(HeaderNames.RpcToolVerison, options.ToolVersion);
        if (!string.IsNullOrEmpty(options.Page)) request.Headers.Add(HeaderNames.RpcPage, options.Page);
        request.Headers.Add("X-Requested-With", "com.mihoyo.hyperion");
        request.Headers.Add(HeaderNames.Origin, UserAgents.WebstaticReferer);
        if (options.ChallengeGame.HasValue) request.Headers.Add(HeaderNames.RpcChallengeGame, options.ChallengeGame.Value.ToString());
        if (!string.IsNullOrEmpty(options.ChallengePath)) request.Headers.Add(HeaderNames.RpcChallengePath, options.ChallengePath);
        if (!string.IsNullOrEmpty(options.Challenge)) request.Headers.Add(HeaderNames.RpcChallenge, options.Challenge);
        request.Headers.Add(HeaderNames.DS, CalculateDs2(options.DsSalt, options.SortedQuery, options.Body));
        request.Headers.Add(HeaderNames.Referer, UserAgents.WebstaticReferer);
        request.Headers.Add(HeaderNames.Accept, "application/json, text/plain, */*");
        request.Headers.UserAgent.ParseAdd(options.UserAgent);
    }
}

/// <summary>game_record 请求头构建参数。</summary>
public sealed record GameRecordHeaderOptions(
    string AppVersion,
    string UserAgent,
    string DeviceId,
    string DeviceFp,
    string DeviceName,
    string SysVersion,
    string Cookie,
    string DsSalt,
    string SortedQuery,
    string Body = "",
    string? Challenge = null,
    int? ChallengeGame = null,
    string? ChallengePath = null,
    string? ToolVersion = null,
    string? Page = null);
