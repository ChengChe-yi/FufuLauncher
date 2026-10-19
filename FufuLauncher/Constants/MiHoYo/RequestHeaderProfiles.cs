/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Constants.MiHoYo;

public enum HeaderProfileKind
{
    Unknown = 0,


    PassportAppQr,


    WebPassport,


    GameQr,


    MiyousheBbs,


    MihoyoCnWeb,


    TakumiRecordCn,


    TakumiAccountCn,


    TakumiEventCn,


    Hoyolab,


    CloudGame,


    Geetest,


    DeviceFingerprint,


    BbsDebugBridge,
}

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

public static class RequestHeaderProfiles
{
    public static readonly HeaderProfile PassportAppQr = new(
        HeaderProfileKind.PassportAppQr,
        HeaderVersions.MobileCnLogin,
        UserAgents.OkHttp,
        ClientTypes.Login,
        GameBizValues.BbsCn,
        AppIds.Passport,
        Channels.MiyousheLuodi,
        null,
        nameof(HeaderSalts.MiyakoApp));


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