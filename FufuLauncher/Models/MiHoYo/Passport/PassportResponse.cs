/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Passport;

public class PassportResponse
{
    [JsonPropertyName("retcode")]
    public int RetCode { get; set; }
    
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
    
    public bool IsSuccess => RetCode == 0;
}

public sealed class PassportResponse<TData> : PassportResponse
{
    [JsonPropertyName("data")]
    public TData? Data { get; set; }
}
