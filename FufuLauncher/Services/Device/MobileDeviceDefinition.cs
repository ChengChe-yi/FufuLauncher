/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FufuLauncher.Services.Device;


public sealed record MobileDeviceDefinition
{


    /// <summary>Android Build.MODEL。</summary>
    public required string Model { get; init; }

    /// <summary>Android Build.PRODUCT。</summary>
    public required string ProductName { get; init; }

    /// <summary>Android Build.BRAND。</summary>
    public required string Brand { get; init; }

    /// <summary>Android Build.MANUFACTURER。</summary>
    public required string Manufacturer { get; init; }

    /// <summary>Android Build.HARDWARE。</summary>
    public required string Hardware { get; init; }

    /// <summary>Android Build.BOARD。</summary>
    public required string Board { get; init; }

    /// <summary>Android Build.DEVICE（ext_fields.deviceType）。</summary>
    public required string DeviceType { get; init; }

    /// <summary>Android Build.FINGERPRINT。</summary>
    public required string DeviceInfo { get; init; }

    /// <summary>Android Build.ID。</summary>
    public required string BuildId { get; init; }

    /// <summary>Android Build.DISPLAY。</summary>
    public required string BuildDisplay { get; init; }

    /// <summary>Android Build.TIME（Unix 毫秒；ROM 编译时间）。</summary>
    public required long BuildTime { get; init; }

    /// <summary>Android Build.HOST（ROM 属性）。</summary>
    public required string Hostname { get; init; }

    /// <summary>Build.VERSION.RELEASE。</summary>
    public required string OsVersion { get; init; }

    /// <summary>Build.VERSION.SDK_INT。</summary>
    public required string SdkVersion { get; init; }

    /// <summary>屏幕分辨率（宽x高）。</summary>
    public required string ScreenSize { get; init; }

    /// <summary>CPU ABI（如 arm64-v8a）。</summary>
    public required string CpuType { get; init; }

    /// <summary>Android Build.VENDOR。</summary>
    public required string Vendor { get; init; }

    /// <summary>Android Build.TAGS。</summary>
    public required string BuildTags { get; init; }

    /// <summary>Android Build.TYPE。</summary>
    public required string BuildType { get; init; }

    /// <summary>Android Build.USER。</summary>
    public required string BuildUser { get; init; }

    /// <summary>Android Build.ID 的短标识。</summary>
    public required string DevId { get; init; }


    /// <summary>Java 堆上限（MB）：<c>romCapacity</c> 与 <c>appMemory</c> 同源。</summary>
    public required int HeapCapMb { get; init; }

    /// <summary>/data 分区总容量（MB）：<c>ramCapacity</c> 与 <c>sdCapacity</c> 同源。</summary>
    public required int DataTotalMb { get; init; }

    /// <summary>同一分区两种视图的固定差值：<c>ramRemain - sdRemain</c>（MB）。</summary>
    public required int SdFreeDeltaMb { get; init; }

    /// <summary>是否平板。</summary>
    public required int IsTablet { get; init; }

    /// <summary>是否带物理键盘。</summary>
    public required int HasKeyboard { get; init; }


    public string? DisplayName { get; init; }

    /// <summary><c>ext_fields.deviceName</c> 取值：显示名，留空回落机型名。</summary>
    public string ResolvedDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? Model : DisplayName!;


    public required string AndroidId { get; init; }

    /// <summary>ANDROID_ID = MD5(机器名 + "FufuLauncher") 前 16 位 hex。</summary>
    public static string CreateAndroidId()
    {
        string raw = Environment.MachineName + "FufuLauncher";
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw)))
                      .ToLower(CultureInfo.InvariantCulture)[..16];
    }

    // ---------------------------------------------------------------- 固定画像

    public static readonly MobileDeviceDefinition Default = new()
    {
        Model = "2605EPN8EC",
        ProductName = "2605EPN8EC",
        Brand = "Xiaomi",
        Manufacturer = "Xiaomi",
        Hardware = "Xiaomi",
        Board = "2605EPN8EC",
        DeviceType = "2605EPN8EC",
        DeviceInfo = "Xiaomi/2605EPN8EC/2605EPN8EC:16/V417IR/1747:user/release-keys",
        BuildId = "V417IR",
        BuildDisplay = "V417IR release-keys",
        BuildTime = 1788507594000L,
        Hostname = "6b29a8384f29",
        OsVersion = "16",
        SdkVersion = "36",
        ScreenSize = "1080x1920",
        CpuType = "arm64-v8a",
        Vendor = "unknown",
        BuildTags = "release-keys",
        BuildType = "user",
        BuildUser = "abc",
        DevId = "REL",
        HeapCapMb = 512,
        DataTotalMb = 2950,
        SdFreeDeltaMb = 170,
        IsTablet = 0,
        HasKeyboard = 1,
        DisplayName = null,
        AndroidId = CreateAndroidId(),
    };
}
