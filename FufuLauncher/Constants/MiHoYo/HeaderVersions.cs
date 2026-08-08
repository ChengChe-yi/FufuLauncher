/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Constants.MiHoYo;

/// <summary>
/// 米游社 / HoYoLAB HTTP 请求中使用的版本号集中定义。
/// 替换散落在各 Service 的私有 const（如 DailyNoteService.CNVersion / GenshinApiClient.CnAppVersion）。
/// </summary>
public static class HeaderVersions
{
    /// <summary>原神国服 BBS 应用版本号（米游社 2.99.1，CommunityCheckinService.cs:215 即 GenshinApiEndpoints.BbsVersion）。</summary>
    public const string BbsCn = "2.99.1";

    /// <summary>原神国服移动端 BBS / fingerprint 应用版本号（DailyNoteService.CNVersion、GeetestService.CNVersion、BBSWindow.CNVersion、DeviceProfileService 默认 2.109.0）。</summary>
    public const string MobileCn = "2.109.0";

    /// <summary>米游社旧扫码客户端版本（LoginQrWindow.xaml.cs:794/836）。</summary>
    public const string MobileCnLegacy = "2.71.1";

    /// <summary>米游社移动端 2.93.1（TokenRefresh / UserInfo 登录路径）。</summary>
    public const string MobileCn293 = "2.93.1";

    /// <summary>原神国服 webstatic mihoyo（DailyNoteService 主链路、GachaService、TokenRefresh）。</summary>
    public const string PcWebCn = "2.90.1";

    /// <summary>HoYoLAB / os 客户端应用版本（HoyolabCheckinService / HoyolabRoleResolverService / GenshinApiClient OsAppVersion）。</summary>
    public const string PcWebOs = "3.13.0";

    /// <summary>米游社 x-rpc-sdk_version（BBSWindow.xaml.cs:357/609 与 LoginQrWindow.xaml.cs:1422 由调用方传入）。</summary>
    public const string SdkVersion = "2.16.0";

    /// <summary>米游社 x-rpc-account_version（LoginQrWindow.xaml.cs:1423）。</summary>
    public const string AccountVersion = "2.90.1";

    /// <summary>
    /// 工具版本号（DailyNoteService header 中的 <c>x-rpc-tool_verison=v6.6.1-gr-cn</c>）。
    /// <para>
    /// 注意：键名 <c>tool_verison</c> 是官方接口使用的拼写，正确的是 <c>tool_version</c>，
    /// 中间第 3 个字母官方写成了 <c>i</c>（应为 <c>o</c>），属于上游拼写错误。
    /// 本常量类为保持与上游兼容，沿用此错拼，请勿更名。
    /// </para>
    /// </summary>
    public const string ToolVersionCn = "v6.6.1-gr-cn";
}