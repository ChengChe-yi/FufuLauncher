/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Passport;

public sealed class Risk
{
    [JsonPropertyName("risk_ticket")]
    public string RiskTicket { get; set; } = "";

    [JsonPropertyName("verify_str")]
    public string? VerifyString { get; set; }
}

public sealed class RiskVerify
{
    [JsonPropertyName("ticket")]
    public string Ticket { get; set; } = "";

    [JsonPropertyName("verify_type")]
    public string VerifyType { get; set; } = "";
}
