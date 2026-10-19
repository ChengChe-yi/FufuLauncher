/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;

namespace FufuLauncher.Services.Device;

public sealed record MobileDeviceSnapshot
{
    public required MobileDeviceDefinition Device
    {
        get;
        init;
    }


    public required long CapturedAtMs
    {
        get;
        init;
    }


    public required int BatteryPercent
    {
        get;
        init;
    }


    public required int HeapFreeMb
    {
        get;
        init;
    }


    public required int DataFreeMb
    {
        get;
        init;
    }


    public required long AppInstallTimeMs
    {
        get;
        init;
    }


    public required string Accelerometer
    {
        get;
        init;
    }


    public required string Gyroscope
    {
        get;
        init;
    }


    public required string Magnetometer
    {
        get;
        init;
    }


    public int SdFreeMb => DataFreeMb - Device.SdFreeDeltaMb;
}

public sealed class MobileDeviceService
{
    private readonly long _appInstallTimeMs =
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Random.Shared.Next(2, 8) * 60_000L;


    public MobileDeviceDefinition Device
    {
        get;
    } = MobileDeviceDefinition.Default;


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
            AppInstallTimeMs = _appInstallTimeMs,
            Accelerometer = Inv(Random.Shared.NextDouble() * 0.8 - 0.4, "F7") + "x"
                                                                              + Inv(
                                                                                  9.8 + Random.Shared.NextDouble() *
                                                                                  0.1 - 0.05, "F7") + "x"
                                                                              + Inv(Random.Shared.NextDouble() * 0.4,
                                                                                  "F7"),
            Gyroscope = Inv(Random.Shared.Next(-6, 7) * 0.0000610, "F7") + "x"
                                                                         + Inv(Random.Shared.Next(-6, 7) * 0.0000610,
                                                                             "F7") + "x"
                                                                         + Inv(Random.Shared.Next(-6, 7) * 0.0000610,
                                                                             "F7"),
            Magnetometer = Inv(Random.Shared.Next(-600, 601) * 0.0625, "F4") + "x"
                                                                             + Inv(
                                                                                 Random.Shared.Next(-600, 601) * 0.0625,
                                                                                 "F4") + "x"
                                                                             + Inv(
                                                                                 Random.Shared.Next(-600, 601) * 0.0625,
                                                                                 "F4"),
        };
    }


    private static string Inv(double value, string format) =>
        value.ToString(format, CultureInfo.InvariantCulture);
}