/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Constants.MiHoYo;

/// <summary>
/// 端点分类（用于 HeaderBuilder 按 family 选取预设值）。
/// </summary>
public enum HeaderProfileKind
{
    /// <summary>未分类的兜底。</summary>
    Unknown = 0,

    /// <summary>米游社 App 扫码（PassportAppCreateQrLoginUrl / QueryQrLoginStatus）。</summary>
    PassportAppQr,

    /// <summary>网页通行证（Passport Create/Scan/Confirm/QueryQr）。</summary>
    WebPassport,

    /// <summary>游戏扫码（Hk4eQrCodeFetch / Hk4eQrCodeQuery / GetTokenByGameToken）。</summary>
    GameQr,

    /// <summary>米游社 BBS 社区（CommunityCheckinService / GenshinApiEndpoints 系列）。</summary>
    MiyousheBbs,

    /// <summary>米游社原神国服 webstatic mihoyo（DailyNoteService / GachaService / TokenRefresh）。</summary>
    MihoyoCnWeb,

    /// <summary>米游社游戏记录 api-takumi-record（GenshinApiClient cn）。</summary>
    TakumiRecordCn,

    /// <summary>米游社账号 / 角色解析 api-takumi（UserInfoService）。</summary>
    TakumiAccountCn,

    /// <summary>米游社游戏事件 api-takumi（TravelersDiary、Luna 签到）。</summary>
    TakumiEventCn,

    /// <summary>HoYoLAB 海外签到 / 角色解析 / 游戏记录。</summary>
    Hoyolab,

    /// <summary>云原神 wallet（CloudGameCheckinService）。</summary>
    CloudGame,

    /// <summary>极验验证（GeetestService.cs）。</summary>
    Geetest,

    /// <summary>设备指纹 device-fp（DeviceFingerprintService）。</summary>
    DeviceFingerprint,

    /// <summary>BBSWindow 调试器（MihoyoBBS.cs 旧 GameCheckin 头）。</summary>
    BbsDebugBridge,
}

/// <summary>
/// 一组端点固定的请求头预设值。
/// 供 HeaderBuilder 按 <see cref="HeaderProfileKind"/> 直接查表消费，
/// 避免业务代码再各自散落拼装版本号 / UA / client_type / game_biz。
/// </summary>
public sealed record HeaderProfile(
    HeaderProfileKind Kind,
    string AppVersion,
    string UserAgent,
    string ClientType,
    string? GameBiz,
    string? AppId,
    string? Channel,
    string? Referer,
    string SaltType);

/// <summary>
/// 按 <see cref="HeaderProfileKind"/> 提供所有常用 Profile 预设。
/// 仅承载静态值，不进行任何 IO / DS 计算。
/// </summary>
public static class RequestHeaderProfiles
{
    /// <summary>Passport App 扫码 — MobileCn293 + OkHttp + Login + bbs_cn + Passport。</summary>
    public static readonly HeaderProfile PassportAppQr = new(
        HeaderProfileKind.PassportAppQr,
        HeaderVersions.MobileCn293,
        UserAgents.OkHttp,
        ClientTypes.Login,
        GameBizValues.BbsCn,
        AppIds.Passport,
        Channels.MiyousheLuodi,
        null,
        nameof(HeaderSalts.MiyakoApp));

    /// <summary>网页通行证 — PcWebCn + Capture + Mobile + bbs_cn + Passport。</summary>
    public static readonly HeaderProfile WebPassport = new(
        HeaderProfileKind.WebPassport,
        HeaderVersions.PcWebCn,
        UserAgents.WindowsBbs290Capture,
        ClientTypes.Mobile,
        GameBizValues.BbsCn,
        AppIds.Passport,
        Channels.MiyousheLuodi,
        null,
        nameof(HeaderSalts.MiyakoApp));

    /// <summary>游戏扫码 — MobileCnLegacy + OkHttp + GameApp + bbs_cn + GameCombo。</summary>
    public static readonly HeaderProfile GameQr = new(
        HeaderProfileKind.GameQr,
        HeaderVersions.MobileCnLegacy,
        UserAgents.OkHttp,
        ClientTypes.GameApp,
        GameBizValues.BbsCn,
        AppIds.GameCombo,
        Channels.MiyousheLuodi,
        null,
        nameof(HeaderSalts.MobileMihoyo));

    /// <summary>米游社 BBS 社区 — BbsCn + OkHttp + Mobile + bbs_cn + null appId。</summary>
    public static readonly HeaderProfile MiyousheBbs = new(
        HeaderProfileKind.MiyousheBbs,
        HeaderVersions.BbsCn,
        UserAgents.OkHttp,
        ClientTypes.Mobile,
        GameBizValues.BbsCn,
        null,
        Channels.MiyousheLuodi,
        null,
        nameof(HeaderSalts.MobileMihoyo));

    /// <summary>米游社国服 webstatic — PcWebCn + Capture + PcWeb + hk4e_cn。</summary>
    public static readonly HeaderProfile MihoyoCnWeb = new(
        HeaderProfileKind.MihoyoCnWeb,
        HeaderVersions.PcWebCn,
        UserAgents.WindowsBbs290Capture,
        ClientTypes.PcWeb,
        GameBizValues.Hk4eCn,
        null,
        null,
        UserAgents.WebstaticReferer,
        nameof(HeaderSalts.CnX4));

    /// <summary>游戏记录 api-takumi-record（cn）— MobileCn + PcWeb + hk4e_cn。</summary>
    public static readonly HeaderProfile TakumiRecordCn = new(
        HeaderProfileKind.TakumiRecordCn,
        HeaderVersions.MobileCn,
        UserAgents.OkHttp,
        ClientTypes.PcWeb,
        GameBizValues.Hk4eCn,
        null,
        null,
        UserAgents.WebstaticReferer,
        nameof(HeaderSalts.CnX6));

    /// <summary>账号 / 角色 api-takumi（cn）— MobileCn293 + PcWeb + hk4e_cn + AndroidBbs293。</summary>
    public static readonly HeaderProfile TakumiAccountCn = new(
        HeaderProfileKind.TakumiAccountCn,
        HeaderVersions.MobileCn293,
        UserAgents.AndroidBbs293,
        ClientTypes.PcWeb,
        GameBizValues.Hk4eCn,
        null,
        null,
        null,
        nameof(HeaderSalts.CnX4));

    /// <summary>米游社游戏事件（Luna 签到、TravelersDiary）— MobileCn + PcWeb + hk4e_cn + signgame。</summary>
    public static readonly HeaderProfile TakumiEventCn = new(
        HeaderProfileKind.TakumiEventCn,
        HeaderVersions.MobileCn,
        UserAgents.AndroidBbs293,
        ClientTypes.PcWeb,
        GameBizValues.Hk4eCn,
        null,
        null,
        null,
        nameof(HeaderSalts.WebMihoyo));

    /// <summary>HoYoLAB — PcWebOs + HoyolabOversea313 + PcWeb + hk4e_global。</summary>
    public static readonly HeaderProfile Hoyolab = new(
        HeaderProfileKind.Hoyolab,
        HeaderVersions.PcWebOs,
        UserAgents.HoyolabOversea313,
        ClientTypes.PcWeb,
        GameBizValues.Hk4eGlobal,
        null,
        null,
        null,
        nameof(HeaderSalts.OsOs0));

    /// <summary>云原神 wallet — PcWebCn + AndroidBbs293 + PcWeb + hk4e_cn + AppMihoyoReferer。</summary>
    public static readonly HeaderProfile CloudGame = new(
        HeaderProfileKind.CloudGame,
        HeaderVersions.PcWebCn,
        UserAgents.AndroidBbs293,
        ClientTypes.PcWeb,
        GameBizValues.Hk4eCn,
        null,
        null,
        UserAgents.AppMihoyoReferer,
        nameof(HeaderSalts.CnX6));

    /// <summary>极验 — MobileCn + PcWeb + bbs_cn。</summary>
    public static readonly HeaderProfile Geetest = new(
        HeaderProfileKind.Geetest,
        HeaderVersions.MobileCn,
        UserAgents.OkHttp,
        ClientTypes.PcWeb,
        GameBizValues.BbsCn,
        null,
        null,
        UserAgents.WebstaticReferer,
        nameof(HeaderSalts.CnX6));

    /// <summary>device-fp 注册 — OkHttp + Mobile + bbs_cn。</summary>
    public static readonly HeaderProfile DeviceFingerprint = new(
        HeaderProfileKind.DeviceFingerprint,
        HeaderVersions.MobileCn293,
        UserAgents.OkHttp,
        ClientTypes.Mobile,
        GameBizValues.BbsCn,
        null,
        null,
        null,
        nameof(HeaderSalts.MobileMihoyo));

    /// <summary>BBSWindow 调试桥 — MobileCn + PcWeb + hk4e_cn + signgame。</summary>
    public static readonly HeaderProfile BbsDebugBridge = new(
        HeaderProfileKind.BbsDebugBridge,
        HeaderVersions.MobileCn,
        UserAgents.AndroidBbs293,
        ClientTypes.PcWeb,
        GameBizValues.Hk4eCn,
        AppIds.Passport,
        null,
        null,
        nameof(HeaderSalts.WebMihoyo));

    /// <summary>按 kind 取默认 Profile。未知 kind 返回 null，由调用方回退到全局兜底。</summary>
    public static HeaderProfile? Get(HeaderProfileKind kind) => kind switch
    {
        HeaderProfileKind.PassportAppQr => PassportAppQr,
        HeaderProfileKind.WebPassport => WebPassport,
        HeaderProfileKind.GameQr => GameQr,
        HeaderProfileKind.MiyousheBbs => MiyousheBbs,
        HeaderProfileKind.MihoyoCnWeb => MihoyoCnWeb,
        HeaderProfileKind.TakumiRecordCn => TakumiRecordCn,
        HeaderProfileKind.TakumiAccountCn => TakumiAccountCn,
        HeaderProfileKind.TakumiEventCn => TakumiEventCn,
        HeaderProfileKind.Hoyolab => Hoyolab,
        HeaderProfileKind.CloudGame => CloudGame,
        HeaderProfileKind.Geetest => Geetest,
        HeaderProfileKind.DeviceFingerprint => DeviceFingerprint,
        HeaderProfileKind.BbsDebugBridge => BbsDebugBridge,
        _ => null,
    };
}