/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Constants.MiHoYo;

public static class HeaderNames
{
    public const string DS = "DS";
    
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
    public const string RpcDeviceModel = "x-rpc-device_model";
    public const string RpcLifecycleId = "x-rpc-lifecycle_id";
    public const string RpcVerifyKey = "x-rpc-verify_key";
    public const string RpcCsmSource = "x-rpc-csm_source";
    public const string RpcH265Supported = "x-rpc-h265_supported";
    public const string RpcLanguage = "x-rpc-language";
    public const string RpcAigis = "x-rpc-aigis";
    public const string RpcVerify = "x-rpc-verify";
    public const string UserAgent = "User-Agent";
    public const string Referer = "Referer";
    public const string Origin = "Origin";
    public const string Accept = "Accept";
    public const string AcceptEncoding = "Accept-Encoding";
    public const string ContentType = "Content-Type";
    public const string Cookie = "Cookie";
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

public static class MediaTypes
{
    public const string Json = "application/json; charset=utf-8";
    public const string Form = "application/x-www-form-urlencoded; charset=utf-8";
    public const string Gzip = "gzip";
    public const string Deflate = "deflate";
}

public static class ClientTypes
{
    public const string IosApp = "1";
    public const string AndroidApp = "2";
    public const string GameApp = "3";
    public const string Web = "4";
    public const string Other = "5";
    public const string Mobile = AndroidApp;
    public const string Login = Web;
    public const string PcWeb = Other;
}

public static class AppIds
{
    public const string Passport = "bll8iq97cem8";
    public const string GameCombo = "ddxf5dufpuyo";
    public const string HoyoPlayOversea = "ddxf6vlr1reo";
}

public static class GameBizValues
{
    public const string Hk4eCn = "hk4e_cn";
    public const string Hk4eGlobal = "hk4e_global";
    public const string BbsCn = "bbs_cn";
    public const string Nation = "hk4e";
}

public static class Channels
{
    public const string MiyousheLuodi = "miyousheluodi";
}

public static class UserAgents
{
    public const string OkHttp = "okhttp/4.9.3";
    public const string WindowsBbs271 = "Mozilla/5.0 miHoYoBBS/2.71.1";
    public const string WindowsBbs290Capture = "Mozilla/5.0 miHoYoBBS/2.90.1 Capture/2.2.0";
    public const string AndroidBbs293 = "Mozilla/5.0 (Linux; Android 12; Unspecified Device) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/103.0.5060.129 Mobile Safari/537.36 miHoYoBBS/2.93.1";
    public const string AndroidBbsTemplate = "Mozilla/5.0 (Linux; Android {0}; {1} Build/{2}; wv) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/110.0.5481.154 Safari/537.36 miHoYoBBS/{3}";
    public const string WindowsBbs295 = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBS/2.95.1";
    public const string HoyoPlay = "HYPContainer/1.1.4.133";
    public const string WindowsBbsOversea254 = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) miHoYoBBSOversea/2.54.0";
    public const string HoyolabOversea313 = "Mozilla/5.0 (Linux; Android 13; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/118.0.0.0 Mobile Safari/537.36 miHoYoBBSOversea/3.13.0";
    public const string WebstaticReferer = "https://webstatic.mihoyo.com";
    public const string AppMihoyoReferer = "https://app.mihoyo.com";
}