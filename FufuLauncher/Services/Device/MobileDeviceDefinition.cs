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
    public required string Model
    {
        get;
        init;
    }


    public required string ProductName
    {
        get;
        init;
    }


    public required string Brand
    {
        get;
        init;
    }


    public required string Manufacturer
    {
        get;
        init;
    }


    public required string Hardware
    {
        get;
        init;
    }


    public required string Board
    {
        get;
        init;
    }


    public required string DeviceType
    {
        get;
        init;
    }


    public required string DeviceInfo
    {
        get;
        init;
    }


    public required string BuildId
    {
        get;
        init;
    }


    public required string BuildDisplay
    {
        get;
        init;
    }


    public required long BuildTime
    {
        get;
        init;
    }


    public required string Hostname
    {
        get;
        init;
    }


    public required string OsVersion
    {
        get;
        init;
    }


    public required string SdkVersion
    {
        get;
        init;
    }


    public required string ScreenSize
    {
        get;
        init;
    }


    public required string CpuType
    {
        get;
        init;
    }


    public required string Vendor
    {
        get;
        init;
    }


    public required string BuildTags
    {
        get;
        init;
    }


    public required string BuildType
    {
        get;
        init;
    }


    public required string BuildUser
    {
        get;
        init;
    }


    public required string DevId
    {
        get;
        init;
    }


    public required int HeapCapMb
    {
        get;
        init;
    }


    public required int DataTotalMb
    {
        get;
        init;
    }


    public required int SdFreeDeltaMb
    {
        get;
        init;
    }


    public required int IsTablet
    {
        get;
        init;
    }


    public required int HasKeyboard
    {
        get;
        init;
    }


    public string? DisplayName
    {
        get;
        init;
    }


    public string ResolvedDisplayName => string.IsNullOrWhiteSpace(DisplayName) ? Model : DisplayName!;


    public required string AndroidId
    {
        get;
        init;
    }


    public static string CreateAndroidId()
    {
        string raw = Environment.MachineName + "FufuLauncher";
        return Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(raw)))
            .ToLower(CultureInfo.InvariantCulture)[..16];
    }


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