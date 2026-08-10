/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Fingerprint;

/// <summary>
/// getFp 响应体 <c>data</c> 段（外层为 <c>{ retcode, message, data }</c>）。
/// </summary>
public sealed record DeviceFpResponse
{
    /// <summary>注册 / 续期后的设备指纹。</summary>
    [JsonPropertyName("device_fp")]
    public string DeviceFp { get; set; } = "";

    /// <summary>指纹服务自身返回码（0 表示成功）。</summary>
    [JsonPropertyName("code")]
    public int Code { get; set; }

    /// <summary>指纹服务错误信息。</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}
