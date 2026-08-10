/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Fingerprint;

/// <summary>
/// getFp 指纹注册 / 续期请求体（POST <c>public-data-api.mihoyo.com/device-fp/api/getFp</c>）。
/// </summary>
public sealed record DeviceFpRequest
{
    /// <summary>设备标识（随机 16 位 hex）。</summary>
    [JsonPropertyName("device_id")]
    public string DeviceId { get; set; } = "";

    /// <summary>随机种子 ID（GUID）。</summary>
    [JsonPropertyName("seed_id")]
    public string SeedId { get; set; } = "";

    /// <summary>随机种子时间（UTC 毫秒）。</summary>
    [JsonPropertyName("seed_time")]
    public string SeedTime { get; set; } = "";

    /// <summary>客户端类型（2 = 安卓 App，5 = 网页 / WebView）。</summary>
    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "";

    /// <summary>已有指纹（续期时携带原值；全新注册为空）。</summary>
    [JsonPropertyName("device_fp")]
    public string DeviceFp { get; set; } = "";

    /// <summary>应用标识（bbs_cn / hk4e_cn）。</summary>
    [JsonPropertyName("app_name")]
    public string AppName { get; set; } = "";

    /// <summary>扩展画像字段（JSON 字符串）。</summary>
    [JsonPropertyName("ext_fields")]
    public string ExtFields { get; set; } = "";

    /// <summary>BBS 设备 ID（与请求头 x-rpc-device_id 同源）。</summary>
    [JsonPropertyName("bbs_device_id")]
    public string? BbsDeviceId { get; set; }
}
