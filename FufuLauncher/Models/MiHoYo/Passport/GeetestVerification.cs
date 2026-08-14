/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Passport;

public sealed class GeetestVerification
{
    [JsonPropertyName("success")]
    public int Success { get; set; }

    [JsonPropertyName("gt")]
    public string Gt { get; set; } = "";

    [JsonPropertyName("challenge")]
    public string Challenge { get; set; } = "";
    
    [JsonPropertyName("new_captcha")]
    public JsonElement NewCaptcha { get; set; }
}
