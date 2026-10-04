/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Globalization;

namespace FufuLauncher.Services.Device;


public sealed record MobileDeviceSnapshot
{
    /// <summary>快照所属的固定设备档案。</summary>
    public required MobileDeviceDefinition Device { get; init; }

    /// <summary>快照采集时刻（Unix 毫秒）。</summary>
    public required long CapturedAtMs { get; init; }

    /// <summary>电量百分比。</summary>
    public required int BatteryPercent { get; init; }

    /// <summary>Java 堆内剩余（MB）。</summary>
    public required int HeapFreeMb { get; init; }

    /// <summary>/data 分区剩余（MB）。</summary>
    public required int DataFreeMb { get; init; }

    /// <summary>应用安装时刻（Unix 毫秒）；全新安装时 install == update。</summary>
    public required long AppInstallTimeMs { get; init; }

    /// <summary>加速度计读数（"x" 分隔三轴，7 位小数）。</summary>
    public required string Accelerometer { get; init; }

    /// <summary>陀螺仪读数（"x" 分隔三轴）。</summary>
    public required string Gyroscope { get; init; }

    /// <summary>磁力计读数（"x" 分隔三轴）。</summary>
    public required string Magnetometer { get; init; }

    /// <summary>
    /// sdcard 视图剩余（MB）：同一分区另一种视图，恒为 <c>DataFreeMb - SdFreeDeltaMb</c>。
    /// <para>派生而非独立随机，以保证该关系恒成立。</para>
    /// </summary>
    public int SdFreeMb => DataFreeMb - Device.SdFreeDeltaMb;
}

/// <summary>
/// 移动端模拟设备的统一出口：固定档案 + 单元级随机采样。
/// <para>不含请求头、UA、设备标识派生或 <c>ext_fields</c> 组装。</para>
/// </summary>
public sealed class MobileDeviceService
{
    /// <summary>固定设备档案。</summary>
    public MobileDeviceDefinition Device { get; } = MobileDeviceDefinition.Default;

    /// <summary>
    /// 采集一份设备信息快照：档案字段固定，单元级字段本次随机取值。
    /// </summary>
    public MobileDeviceSnapshot CaptureSnapshot()
    {
        long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        return new MobileDeviceSnapshot
        {
            Device = Device,
            CapturedAtMs = nowMs,
            BatteryPercent = Random.Shared.Next(40, 96),
            HeapFreeMb = Random.Shared.Next(392, 492),
            DataFreeMb = Random.Shared.Next(2164, 2589),
            AppInstallTimeMs = nowMs - Random.Shared.Next(2, 8) * 60_000L,
            Accelerometer = Inv(Random.Shared.NextDouble() * 0.8 - 0.4, "F7") + "x"
                + Inv(9.8 + Random.Shared.NextDouble() * 0.1 - 0.05, "F7") + "x"
                + Inv(Random.Shared.NextDouble() * 0.4, "F7"),
            Gyroscope = Inv(Random.Shared.Next(-6, 7) * 0.0000610, "F7") + "x"
                + Inv(Random.Shared.Next(-6, 7) * 0.0000610, "F7") + "x"
                + Inv(Random.Shared.Next(-6, 7) * 0.0000610, "F7"),
            Magnetometer = Inv(Random.Shared.Next(-600, 601) * 0.0625, "F4") + "x"
                + Inv(Random.Shared.Next(-600, 601) * 0.0625, "F4") + "x"
                + Inv(Random.Shared.Next(-600, 601) * 0.0625, "F4"),
        };
    }

    /// <summary>不变文化格式化（传感器读数等，避免区域设置差异）。</summary>
    private static string Inv(double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);
}
