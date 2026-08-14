/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Passport;

public sealed class LoginResult
{
    [JsonPropertyName("token")]
    public TokenWrapper? Token { get; set; }

    [JsonPropertyName("user_info")]
    public UserInformation? UserInfo { get; set; }

    [JsonPropertyName("reactivate_info")]
    public ReactivateInfo ReactivateInfo { get; set; } = new();

    [JsonPropertyName("login_ticket")]
    public string LoginTicket { get; set; } = "";
}

public sealed class TokenWrapper
{
    [JsonPropertyName("token_type")]
    public int TokenType { get; set; }

    [JsonPropertyName("token")]
    public string Token { get; set; } = "";
}

public sealed class UserInformation
{
    [JsonPropertyName("aid")]
    public string Aid { get; set; } = "";

    [JsonPropertyName("mid")]
    public string Mid { get; set; } = "";

    [JsonPropertyName("account_name")]
    public string AccountName { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";
    
    [JsonPropertyName("is_email_verify")]
    public JsonElement IsEmailVerify { get; set; }

    [JsonPropertyName("area_code")]
    public string AreaCode { get; set; } = "";

    [JsonPropertyName("mobile")]
    public string Mobile { get; set; } = "";

    [JsonPropertyName("safe_area_code")]
    public string SafeAreaCode { get; set; } = "";

    [JsonPropertyName("safe_mobile")]
    public string SafeMobile { get; set; } = "";

    [JsonPropertyName("realname")]
    public string Realname { get; set; } = "";

    [JsonPropertyName("identity_code")]
    public string IdentityCode { get; set; } = "";

    [JsonPropertyName("rebind_area_code")]
    public string RebindAreaCode { get; set; } = "";

    [JsonPropertyName("rebind_mobile")]
    public string RebindMobile { get; set; } = "";

    [JsonPropertyName("rebind_mobile_time")]
    public string RebindMobileTime { get; set; } = "";

    [JsonPropertyName("links")]
    public List<Link> Links { get; set; } = new();
}

public sealed class ReactivateInfo
{
    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("ticket")]
    public string Ticket { get; set; } = "";
}

public sealed class Link
{
    [JsonPropertyName("thirdparty")]
    public string Thirdparty { get; set; } = "";

    [JsonPropertyName("union_id")]
    public string UnionId { get; set; } = "";

    [JsonPropertyName("nickname")]
    public string Nickname { get; set; } = "";
}
