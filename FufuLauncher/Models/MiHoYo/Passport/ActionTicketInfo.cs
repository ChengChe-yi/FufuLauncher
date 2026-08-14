/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Passport;

public sealed class ActionTicketInfo
{
    [JsonPropertyName("action_ticket")]
    public string ActionTicket { get; set; } = "";

    [JsonPropertyName("verify_info")]
    public VerifyInfo VerifyInfo { get; set; } = new();

    [JsonPropertyName("user_info")]
    public UserInformation UserInfo { get; set; } = new();
    
    [JsonPropertyName("captcha_sent")]
    public bool CaptchaSent { get; set; }
}

public sealed class VerifyInfo
{
    [JsonPropertyName("status")]
    [JsonConverter(typeof(JsonStringEnumConverter<VerifyStatus>))]
    public VerifyStatus Status { get; set; }

    [JsonPropertyName("verify_method_combinations")]
    public List<VerifyMethodsWrapper> VerifyMethodCombinations { get; set; } = new();

    [JsonPropertyName("chosen_methods")]
    public List<int> ChosenMethods { get; set; } = new();

    [JsonPropertyName("partly_verified_methods")]
    public List<int> PartlyVerifiedMethods { get; set; } = new();
}

public sealed class VerifyMethodsWrapper
{
    [JsonPropertyName("verify_methods")]
    public List<int> VerifyMethods { get; set; } = new();
}

public enum VerifyStatus
{
    StatusNew,
    StatusVerified,
}

public sealed class ActionTicketInfoRequest
{
    [JsonPropertyName("action_type")]
    public string ActionType { get; set; } = "verify_for_component";

    [JsonPropertyName("action_ticket")]
    public string ActionTicket { get; set; } = "";

    [JsonPropertyName("email_captcha")]
    public string? EmailCaptcha { get; set; }

    [JsonPropertyName("verify_method")]
    public int? VerifyMethod { get; set; }
}
