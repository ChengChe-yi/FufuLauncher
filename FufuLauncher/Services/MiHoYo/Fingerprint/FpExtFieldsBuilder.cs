/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using FufuLauncher.Models.MiHoYo.Fingerprint;
using FufuLauncher.Services.Device;

namespace FufuLauncher.Services.MiHoYo.Fingerprint;

/// <summary>
/// 指纹专用 <c>ext_fields</c> 组装：设备档案（ROM 级，恒定）+ 设备快照（单元级，每枚换新）。
/// <para>只做映射与按 <c>ext_list</c> 过滤；不随机取值、不发请求、不持久化。</para>
/// </summary>
public static class FpExtFieldsBuilder
{
    /// <summary>设备标识不可用时的错误码（<c>OAIDErrorCode.ERROR_NOT_SUPPORT</c>）。</summary>
    public const string DeviceIdErrorCode = "error_1008005";

    /// <summary>宿主包名（米游社 App）。</summary>
    public const string HostPackageName = "com.mihoyo.hyperion";

    /// <summary>宿主包版本。</summary>
    public const string HostPackageVersion = "2.42.0";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>
    /// 组装完整 <c>ext_fields</c> 画像。
    /// <para>恒等关系在此保证：<c>romCapacity == appMemory</c>、
    /// <c>ramCapacity == sdCapacity</c>、<c>ramRemain - sdRemain == SdFreeDeltaMb</c>。</para>
    /// </summary>
    public static ExtFields FromSnapshot(MobileDeviceSnapshot snapshot)
    {
        var d = snapshot.Device;

        return new ExtFields
        {
            // 机型标识
            Model = d.Model,
            ProductName = d.ProductName,
            Brand = d.Brand,
            Manufacturer = d.Manufacturer,
            Hardware = d.Hardware,
            Board = d.Board,
            DeviceType = d.DeviceType,
            DeviceInfo = d.DeviceInfo,
            OsVersion = d.OsVersion,
            SdkVersion = d.SdkVersion,
            CpuType = d.CpuType,
            Vendor = d.Vendor,
            ScreenSize = d.ScreenSize,

            // Build.*
            BuildTime = d.BuildTime.ToString(),
            Display = d.BuildDisplay,
            BuildTags = d.BuildTags,
            BuildType = d.BuildType,
            BuildUser = d.BuildUser,
            DevId = d.DevId,
            Hostname = d.Hostname,

            // 宿主应用标识（与设备无关，属指纹注册身份）
            PackageName = HostPackageName,
            PackageVersion = HostPackageVersion,

            // 设备显示名（默认等于机型名，用户可改）
            DeviceName = d.ResolvedDisplayName,

            // 堆与分区几何（容量恒定）
            RomCapacity = d.HeapCapMb.ToString(),
            AppMemory = d.HeapCapMb.ToString(),
            RamCapacity = d.DataTotalMb.ToString(),
            SdCapacity = d.DataTotalMb,

            // 堆与分区几何（剩余量随快照）
            RomRemain = snapshot.HeapFreeMb.ToString(),
            RamRemain = snapshot.DataFreeMb.ToString(),
            SdRemain = snapshot.SdFreeMb,

            // 应用安装时刻（绝对 epoch 毫秒；全新安装时 install == update）
            AppInstallTimeDiff = snapshot.AppInstallTimeMs,
            AppUpdateTimeDiff = snapshot.AppInstallTimeMs,

            // 单元级采集量
            BatteryStatus = snapshot.BatteryPercent,
            Accelerometer = snapshot.Accelerometer,
            Gyroscope = snapshot.Gyroscope,
            Magnetometer = snapshot.Magnetometer,

            // 形态
            IsTablet = d.IsTablet,
            HasKeyboard = d.HasKeyboard,

            // 环境常量
            ProxyStatus = 1,
            IsRoot = 0,
            DebugStatus = 0,
            EmulatorStatus = 0,
            IsMockLocation = 0,
            IsAirMode = 0,
            RingMode = 2,
            SimState = 5,
            UiMode = "UI_MODE_TYPE_NORMAL",
            ChargeStatus = 1,
            NetworkType = "WiFi",
            Aaid = DeviceIdErrorCode,
            Vaid = DeviceIdErrorCode,
            Oaid = DeviceIdErrorCode,
        };
    }

    /// <summary>组装并按 <paramref name="allowedKeys"/> 白名单过滤；白名单为空表示不过滤。</summary>
    public static Dictionary<string, object> BuildFiltered(
        MobileDeviceSnapshot snapshot,
        IReadOnlySet<string>? allowedKeys)
    {
        var all = ToFieldDictionary(FromSnapshot(snapshot));

        if (allowedKeys is null || allowedKeys.Count == 0)
        {
            return all;
        }

        return all.Where(kv => allowedKeys.Contains(kv.Key))
                  .ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>组装并序列化为 JSON 字符串（即请求体 <c>ext_fields</c> 的值）。</summary>
    public static string BuildJson(
        MobileDeviceSnapshot snapshot,
        IReadOnlySet<string>? allowedKeys = null) =>
        JsonSerializer.Serialize(BuildFiltered(snapshot, allowedKeys), JsonOptions);

    /// <summary>
    /// 摊平为「JSON 字段名 → 值」字典；键名取自序列化结果，与请求体实际发出的键一致。
    /// </summary>
    public static Dictionary<string, object> ToFieldDictionary(ExtFields fields)
    {
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(fields, JsonOptions));
        var result = new Dictionary<string, object>();
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.Clone();
        }

        return result;
    }
}
