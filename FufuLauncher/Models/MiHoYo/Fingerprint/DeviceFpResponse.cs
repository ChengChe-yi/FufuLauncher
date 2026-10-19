/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Fingerprint;

public sealed record DeviceFpResponse
{
    [JsonPropertyName("device_fp")]
    public string DeviceFp
    {
        get;
        set;
    } = "";


    [JsonPropertyName("code")]
    public int Code
    {
        get;
        set;
    }


    [JsonPropertyName("message")]
    public string Message
    {
        get;
        set;
    } = "";
}