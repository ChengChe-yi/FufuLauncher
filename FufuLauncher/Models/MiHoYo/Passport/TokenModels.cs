/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Passport;

public sealed class STokenWrapper
{
    public STokenWrapper(string stoken, string uid)
    {
        SToken = stoken;
        Uid = uid;
    }

    [JsonPropertyName("stoken")]
    public string SToken { get; set; }

    [JsonPropertyName("uid")]
    public string Uid { get; set; }
}

public sealed class LTokenWrapper
{
    [JsonPropertyName("ltoken")]
    public string LToken { get; set; } = "";
}

public sealed class UidCookieToken
{
    [JsonPropertyName("uid")]
    public string Uid { get; set; } = "";

    [JsonPropertyName("cookie_token")]
    public string CookieToken { get; set; } = "";
}
