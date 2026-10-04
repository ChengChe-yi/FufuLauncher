/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Identity;


public sealed record MiHoYoDeviceIdentity
{
    /// <summary>当前存储格式版本。</summary>
    public const int CurrentVersion = 1;

    /// <summary>存储格式版本。</summary>
    [JsonPropertyName("version")]
    public int Version { get; init; } = CurrentVersion;

    /// <summary>设备号（16 位小写 hex）。</summary>
    [JsonPropertyName("device_id")]
    public string DeviceId { get; init; } = "";

    /// <summary>服务端签发的设备指纹。</summary>
    [JsonPropertyName("device_fp")]
    public string DeviceFp { get; init; } = "";

    /// <summary>随机种子 ID。</summary>
    [JsonPropertyName("seed_id")]
    public string SeedId { get; init; } = "";

    /// <summary>随机种子时间（Unix 毫秒字符串）。</summary>
    [JsonPropertyName("seed_time")]
    public string SeedTime { get; init; } = "";

    /// <summary><c>bbs_device_id</c>：由 <see cref="DeviceId"/> 派生（name-based UUID）。</summary>
    [JsonIgnore]
    public string BbsDeviceId => NameUuidFromBytes(Encoding.UTF8.GetBytes(DeviceId)).ToString();


    [JsonIgnore]
    public bool IsUsable =>
        !string.IsNullOrWhiteSpace(DeviceId)
        && !string.IsNullOrWhiteSpace(SeedId)
        && !string.IsNullOrWhiteSpace(SeedTime);

    /// <summary>生成一份全新身份（<c>device_fp</c> 待注册后回填）。</summary>
    public static MiHoYoDeviceIdentity CreateNew() => new()
    {
        DeviceId = Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant(),
        SeedId = Guid.NewGuid().ToString(),
        SeedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(),
    };

    /// <summary>RFC 4122 name-based UUID（MD5），等价于 Android 侧 nameUUIDFromBytes。</summary>
    private static Guid NameUuidFromBytes(byte[] name)
    {
        var hash = MD5.HashData(name);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        Array.Reverse(hash, 0, 4);
        Array.Reverse(hash, 4, 2);
        Array.Reverse(hash, 6, 2);
        return new Guid(hash);
    }
}
