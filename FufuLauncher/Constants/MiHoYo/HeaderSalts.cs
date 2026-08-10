/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Constants.MiHoYo;

/// <summary>
/// DS 签名 salt 常量集中定义。
/// 替换散落在 Services/Views 中的 private const string Salt / SaltGame / DsSalt / CnSalt / OsSalt。
/// </summary>
public static class HeaderSalts
{
    /// <summary>米游社 App（扫码 / 通行证）— 16.0.1+ 取值（LoginQrWindow.Salt / TokenRefreshService.Salt = "dDIQHbKOdaPaLuvQKVzUzqdeCaxjtaPV"）。</summary>
    public const string MiyakoApp = "dDIQHbKOdaPaLuvQKVzUzqdeCaxjtaPV";

    /// <summary>米游社 Web（web/2）— 旧版通用 web DS（TokenRefreshService.WebSalt = "G1ktdwFL4IyGkHuuWSmz0wUe9Db9scyK"）。</summary>
    public const string WebMihoyo = "G1ktdwFL4IyGkHuuWSmz0wUe9Db9scyK";

    /// <summary>米游社移动端 DS（即 GenshinApiEndpoints.BbsX6Salt；LoginQrWindow.SaltGame = "t0qEgfub6cvueAPgR5m9aQWWVciEer7v"）。</summary>
    public const string MobileMihoyo = "t0qEgfub6cvueAPgR5m9aQWWVciEer7v";

    /// <summary>原神国服 CN x4 DS（GenshinApiClient.CnSalt = "xV8v4Qu54lUKrEYFZkJhB8cuOh9Asafs"）。</summary>
    public const string CnX4 = "xV8v4Qu54lUKrEYFZkJhB8cuOh9Asafs";

    /// <summary>原神国服 CN x6 DS（MiHoYo/DailyNoteService.cs:170 CalculateDS2 与 CommunityCheckinService：MobileMihoyo 同值）。</summary>
    public const string CnX6 = MobileMihoyo;

    /// <summary>原神 os / HoYoLAB 主 DS（GenshinApiClient.OsSalt = HoyolabCheckinService.DsSalt2 = "h4c1d6ywfq5bsbnbhm1bzq7bxzzv6srt"）。</summary>
    public const string OsOs0 = "h4c1d6ywfq5bsbnbhm1bzq7bxzzv6srt";

    /// <summary>HoYoLAB 签到 旧版 DS（HoyolabCheckinService.DsSalt = "okr4obncj8bw5a65hbnn5oo6ixjc3l9w"）。</summary>
    public const string HoyolabLegacy = "okr4obncj8bw5a65hbnn5oo6ixjc3l9w";

    /// <summary>原神抽卡 Lk2 DS（GachaService.Lk2Salt）。</summary>
    public const string GachaLk2 = "sidQFEglajEz7FA0Aj7HQPV88zpf17SO";

    /// <summary>
    /// passport / 账号体系接口 DS salt（配 DS1 生成算法 <c>md5(salt&amp;t&amp;r&amp;b&amp;q)</c>，
    /// ds 形如 <c>t,r,md5</c>，salt 仅参与计算不出现）。
    /// </summary>
    public const string PassportProd = "JwYDpKvLj6MrMqqYU6jTKF17KNO2PXoS";

    /// <summary>
    /// 米游社 App 2.112.0 的 K2 盐（client_type=2，配 DS1 算法 <c>md5(salt&amp;t&amp;r)</c>）。
    /// <para>K2/LK2 随米游社版本变化，升级版本时需同步更新。</para>
    /// </summary>
    public const string K2_2112 = "5e54bba5a8acdf5981ae2c95e528d56f";

    /// <summary>
    /// 米游社 App 2.112.0 的 LK2 盐（client_type=4，配 DS1 算法 <c>md5(salt&amp;t&amp;r)</c>）。
    /// <para>同 <see cref="K2_2112"/>：随版本变化，升级版本时需同步更新。</para>
    /// </summary>
    public const string LK2_2112 = "720eebad04f745764ea4413fe603f3a9";

    /// <summary>
    /// UserInfoService.cs:60 中疑似 typo 的旧 salt 字符串，仅用于排查历史问题，<b>不要用于新调用方</b>。
    /// 与 <see cref="CnX4"/> 的差异在末尾 <c>MXmz9</c> vs <c>Asafs</c>。
    /// </summary>
    public const string UserInfoServiceLegacySalt = "xV8v4Qu54lUKrEYFZkJhB8cuoh9NXmz9";
}