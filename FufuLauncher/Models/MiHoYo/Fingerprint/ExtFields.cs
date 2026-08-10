/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Fingerprint;

/// <summary>
/// getFp 请求体 <c>ext_fields</c> 扩展画像字段（bbs_cn 原生平台）。
/// </summary>
public sealed record ExtFields
{
    [JsonPropertyName("proxyStatus")]
    public int ProxyStatus { get; set; }

    [JsonPropertyName("isRoot")]
    public int IsRoot { get; set; }

    [JsonPropertyName("romCapacity")]
    public string RomCapacity { get; set; } = "512";

    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = "Xiaomi 17 Max";

    [JsonPropertyName("productName")]
    public string ProductName { get; set; } = "2605EPN8EC";

    [JsonPropertyName("romRemain")]
    public string RomRemain { get; set; } = "447";

    [JsonPropertyName("hostname")]
    public string Hostname { get; set; } = "6b29a8384f29";

    [JsonPropertyName("screenSize")]
    public string ScreenSize { get; set; } = "1200x2608";

    [JsonPropertyName("isTablet")]
    public int IsTablet { get; set; } = 0;

    [JsonPropertyName("aaid")]
    public string Aaid { get; set; } = "error_1008008";

    [JsonPropertyName("model")]
    public string Model { get; set; } = "2605EPN8EC";

    [JsonPropertyName("brand")]
    public string Brand { get; set; } = "Xiaomi";

    [JsonPropertyName("hardware")]
    public string Hardware { get; set; } = "Xiaomi";

    [JsonPropertyName("deviceType")]
    public string DeviceType { get; set; } = "2605EPN8EC";

    [JsonPropertyName("devId")]
    public string DevId { get; set; } = "REL";

    [JsonPropertyName("sdCapacity")]
    public int SdCapacity { get; set; }

    [JsonPropertyName("buildTime")]
    public string BuildTime { get; set; } = "1779448087000";

    // 对应 Android Build.USER
    [JsonPropertyName("buildUser")]
    public string BuildUser { get; set; } = "abc";

    [JsonPropertyName("simState")]
    public int SimState { get; set; } = 5;

    [JsonPropertyName("ramRemain")]
    public string RamRemain { get; set; } = "8192";

    /// <summary>
    /// 对应 PackageInfo.lastUpdateTime（绝对毫秒时间戳，非差值）。
    /// <para>生成注册请求体时随机：安装时间之后、当前时间之前。</para>
    /// </summary>
    [JsonPropertyName("appUpdateTimeDiff")]
    public long AppUpdateTimeDiff { get; set; } = 1785998383082L;

    // 对应 Android Build.FINGERPRINT
    [JsonPropertyName("deviceInfo")]
    public string DeviceInfo { get; set; } = "Xiaomi/2605EPN8EC/2605EPN8EC:16/V417IR/1747:user/release-keys";

    [JsonPropertyName("vaid")]
    public string Vaid { get; set; } = "error_1008008";

    [JsonPropertyName("buildType")]
    public string BuildType { get; set; } = "user";

    [JsonPropertyName("sdkVersion")]
    public string SdkVersion { get; set; } = "36";

    [JsonPropertyName("ui_mode")]
    public string UiMode { get; set; } = "UI_MODE_TYPE_NORMAL";

    [JsonPropertyName("isMockLocation")]
    public int IsMockLocation { get; set; }

    [JsonPropertyName("cpuType")]
    public string CpuType { get; set; } = "arm64-v8a";

    [JsonPropertyName("isAirMode")]
    public int IsAirMode { get; set; }

    [JsonPropertyName("ringMode")]
    public int RingMode { get; set; } = 2;

    [JsonPropertyName("chargeStatus")]
    public int ChargeStatus { get; set; } = 1;

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = "Xiaomi";

    [JsonPropertyName("emulatorStatus")]
    public int EmulatorStatus { get; set; }

    [JsonPropertyName("appMemory")]
    public string AppMemory { get; set; } = "512";

    [JsonPropertyName("osVersion")]
    public string OsVersion { get; set; } = "16";

    [JsonPropertyName("vendor")]
    public string Vendor { get; set; } = "unknown";

    [JsonPropertyName("accelerometer")]
    public string Accelerometer { get; set; } = "0.10001241x9.800007x0.1999938";

    [JsonPropertyName("sdRemain")]
    public int SdRemain { get; set; } = 120516;

    [JsonPropertyName("buildTags")]
    public string BuildTags { get; set; } = "release-keys";

    [JsonPropertyName("packageName")]
    public string PackageName { get; set; } = "com.mihoyo.hyperion";

    [JsonPropertyName("networkType")]
    public string NetworkType { get; set; } = "WiFi";

    [JsonPropertyName("oaid")]
    public string Oaid { get; set; } = "error_1008008";

    [JsonPropertyName("debugStatus")]
    public int DebugStatus { get; set; }

    [JsonPropertyName("ramCapacity")]
    public string RamCapacity { get; set; } = "16384";

    [JsonPropertyName("magnetometer")]
    public string Magnetometer { get; set; } = "15.625x-28.25x-32.625";

    // 对应 Android Build.DISPLAY
    [JsonPropertyName("display")]
    public string Display { get; set; } = "V417IR release-keys";

    /// <summary>
    /// 对应 PackageInfo.firstInstallTime（绝对毫秒时间戳，非差值）。
    /// <para>生成注册请求体时随机：晚于设备固件 buildTime、早于 lastUpdateTime。</para>
    /// </summary>
    [JsonPropertyName("appInstallTimeDiff")]
    public long AppInstallTimeDiff { get; set; } = 1785998383082L;

    [JsonPropertyName("packageVersion")]
    public string PackageVersion { get; set; } = "2.42.0";

    [JsonPropertyName("gyroscope")]
    public string Gyroscope { get; set; } = "0.0x0.0x0.0";

    [JsonPropertyName("batteryStatus")]
    public int BatteryStatus { get; set; } = 99;

    [JsonPropertyName("hasKeyboard")]
    public int HasKeyboard { get; set; } = 0;

    // 对应 Android Build.BOARD
    [JsonPropertyName("board")]
    public string Board { get; set; } = "2605EPN8EC";
}
