/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Fingerprint;

public sealed record DeviceFpRequest
{
    [JsonPropertyName("device_id")]
    public string DeviceId
    {
        get;
        set;
    } = "";


    [JsonPropertyName("seed_id")]
    public string SeedId
    {
        get;
        set;
    } = "";


    [JsonPropertyName("seed_time")]
    public string SeedTime
    {
        get;
        set;
    } = "";


    [JsonPropertyName("platform")]
    public string Platform
    {
        get;
        set;
    } = "";


    [JsonPropertyName("device_fp")]
    public string DeviceFp
    {
        get;
        set;
    } = "";


    [JsonPropertyName("app_name")]
    public string AppName
    {
        get;
        set;
    } = "";


    [JsonPropertyName("ext_fields")]
    public string ExtFields
    {
        get;
        set;
    } = "";


    [JsonPropertyName("bbs_device_id")]
    public string? BbsDeviceId
    {
        get;
        set;
    }
}