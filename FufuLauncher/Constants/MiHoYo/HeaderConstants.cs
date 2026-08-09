/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Constants.MiHoYo;

/// <summary>
/// 米游社 / HoYoLAB HTTP 请求头的键名集中定义。
/// 用于替换散落在 Services / Views 中的字符串字面量。
/// </summary>
public static class HeaderNames
{
    /// <summary>动态密钥 / DS 签名。</summary>
    public const string DS = "DS";

    /// <summary>x-rpc-* 系列。</summary>
    public const string RpcAppVersion = "x-rpc-app_version";
    public const string RpcSdkVersion = "x-rpc-sdk_version";
    public const string RpcAccountVersion = "x-rpc-account_version";
    public const string RpcClientType = "x-rpc-client_type";
    public const string RpcDeviceId = "x-rpc-device_id";
    public const string RpcDeviceFp = "x-rpc-device_fp";
    public const string RpcAppId = "x-rpc-app_id";
    public const string RpcChannel = "x-rpc-channel";
    public const string RpcGameBiz = "x-rpc-game_biz";
    public const string RpcSigngame = "x-rpc-signgame";
    public const string RpcSysVersion = "x-rpc-sys_version";
    public const string RpcPlatform = "x-rpc-platform";
    public const string RpcSource = "x-rpc-source";
    public const string RpcToolVerison = "x-rpc-tool_verison";
    public const string RpcPage = "x-rpc-page";
    public const string RpcChallenge = "x-rpc-challenge";
    public const string RpcChallengeGame = "x-rpc-challenge_game";
    public const string RpcChallengePath = "x-rpc-challenge_path";
    public const string RpcDeviceName = "x-rpc-device_name";

    /// <summary>标准头。</summary>
    public const string UserAgent = "User-Agent";
    public const string Referer = "Referer";
    public const string Origin = "Origin";
    public const string Accept = "Accept";
    public const string AcceptEncoding = "Accept-Encoding";
    public const string ContentType = "Content-Type";
    public const string Cookie = "Cookie";

    /// <summary>Cookie 名（DS 签名 / fingerprint 计算使用）。</summary>
    public const string CookieDeviceFp = "DEVICEFP";
    public const string CookieSeedId = "SEED_ID";
    public const string CookieSeedTime = "SEED_TIME";
    public const string CookieAccountId = "account_id";
    public const string CookieAccountIdV2 = "account_id_v2";
    public const string CookieLtuid = "ltuid";
    public const string CookieLtuidV2 = "ltuid_v2";
    public const string CookieLtoken = "ltoken";
    public const string CookieLtokenV2 = "ltoken_v2";
    public const string CookieCookieToken = "cookie_token";
    public const string CookieCookieTokenV2 = "cookie_token_v2";
    public const string CookieStoken = "stoken";
    public const string CookieStokenV2 = "stoken_v2";
    public const string CookieStuid = "stuid";
    public const string CookieMid = "mid";
}

/// <summary>
/// 通用内容类型与编码常量。
/// </summary>
public static class MediaTypes
{
    public const string Json = "application/json; charset=utf-8";
    public const string Form = "application/x-www-form-urlencoded; charset=utf-8";
    public const string Gzip = "gzip";
    public const string Deflate = "deflate";
}

/// <summary>
/// x-rpc-client_type 数值常量。
/// <para>
/// 注：以下列表只是说明该请求头的值通常在哪些平台出现，并不是平台一定只使用对应的值。
/// 具体的值请查看接口说明的标注；DS 字段相关接口会进行标注，因为不同的
/// <c>x-rpc-client_type</c> 对应该版本米游社的 salt 也不同。
/// </para>
/// </summary>
public static class ClientTypes
{
    /// <summary>1：苹果端 APP（一般不需要使用）。</summary>
    public const string IosApp = "1";

    /// <summary>2：安卓端 APP。</summary>
    public const string AndroidApp = "2";

    /// <summary>3：游戏内 / hk4e combo 等扩展用途（项目内用于游戏扫码 GameQr）。</summary>
    public const string GameApp = "3";

    /// <summary>4：网页端。</summary>
    public const string Web = "4";

    /// <summary>5：其它（项目内主要用作 PC Web / webstatic mihoyo 的 client_type）。</summary>
    public const string Other = "5";

    /// <summary>兼容旧引用：移动端 = 2（安卓端 APP）。</summary>
    public const string Mobile = AndroidApp;

    /// <summary>兼容旧引用：登录扫码 = 4（网页端）。</summary>
    public const string Login = Web;

    /// <summary>兼容旧引用：PC Web = 5（其它）。</summary>
    public const string PcWeb = Other;
}

/// <summary>
/// x-rpc-app_id 常量。区分通行证与游戏扫码两个 app_id。
/// </summary>
public static class AppIds
{
    /// <summary>米游社通行证（PassportAppId）。</summary>
    public const string Passport = "bll8iq97cem8";

    /// <summary>游戏扫码 (hk4e combo)。</summary>
    public const string GameCombo = "ddxf5dufpuyo";
}

/// <summary>
/// x-rpc-game_biz 枚举常量。
/// </summary>
public static class GameBizValues
{
    public const string Hk4eCn = "hk4e_cn";
    public const string Hk4eGlobal = "hk4e_global";
    public const string BbsCn = "bbs_cn";
    public const string Nation = "hk4e";
}

/// <summary>
/// x-rpc-channel 枚举常量。
/// </summary>
public static class Channels
{
    public const string MiyousheLuodi = "miyousheluodi";
}

/// <summary>
/// User-Agent 集中管理。
/// </summary>
public static class UserAgents
{
    /// <summary>Android OkHttp 客户端 UA（fingerprint / BBS API）。</summary>
    public const string OkHttp = "okhttp/4.9.3";

    /// <summary>Windows miHoYoBBS 2.71.1（旧扫码 UA，LoginQrWindow.xaml.cs:794/836）。</summary>
    public const string WindowsBbs271 = "Mozilla/5.0 miHoYoBBS/2.71.1";

    /// <summary>Windows miHoYoBBS 2.90.1（Capture/2.2.0，TokenRefresh / LoginQr V2）。</summary>
    public const string WindowsBbs290Capture = "Mozilla/5.0 miHoYoBBS/2.90.1 Capture/2.2.0";

    /// <summary>Android miHoYoBBS 2.93.1（TokenRefresh / UserInfo 默认 UA）。</summary>
    public const string AndroidBbs293 = "Mozilla/5.0 (Linux; Android 12; Unspecified Device) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/103.0.5060.129 Mobile Safari/537.36 miHoYoBBS/2.93.1";

    /// <summary>Android miHoYoBBS 2.109.0（DeviceProfileService 默认模板）。</summary>
    public const string AndroidBbsTemplate2109 = "Mozilla/5.0 (Linux; Android {0}; {1} Build/{2}; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/110.0.5481.154 Safari/537.36 miHoYoBBS/{3}";

    /// <summary>HoYoLAB 海外版 UA（HoyolabCheckinService）。</summary>
    public const string HoyolabOversea313 = "Mozilla/5.0 (Linux; Android 13; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/118.0.0.0 Mobile Safari/537.36 miHoYoBBSOversea/3.13.0";

    /// <summary>Referer: webstatic mihoyo（DailyNote 等）。</summary>
    public const string WebstaticReferer = "https://webstatic.mihoyo.com";

    /// <summary>Referer: app mihoyo（Cloud Game）。</summary>
    public const string AppMihoyoReferer = "https://app.mihoyo.com";
}