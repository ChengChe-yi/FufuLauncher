/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Passport;

public sealed class MobileCaptcha
{
    [JsonPropertyName("sent_new")]
    public bool SentNew { get; set; }
    [JsonPropertyName("countdown")]
    public int Countdown { get; set; }
    [JsonPropertyName("action_type")]
    public string ActionType { get; set; } = "";
}
