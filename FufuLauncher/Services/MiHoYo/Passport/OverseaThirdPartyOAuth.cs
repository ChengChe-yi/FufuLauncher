/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text;
using FufuLauncher.Constants;
using FufuLauncher.Models.MiHoYo.Passport;

namespace FufuLauncher.Services.MiHoYo.Passport;

public static class OverseaThirdPartyOAuth
{
    public static string GetTypeCode(OverseaThirdPartyKind kind) => kind switch
    {
        OverseaThirdPartyKind.Google => "gl",
        OverseaThirdPartyKind.Apple => "ap",
        OverseaThirdPartyKind.Facebook => "fb",
        OverseaThirdPartyKind.Twitter => "tw",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
    
    public static string GetClientId(OverseaThirdPartyKind kind) => kind switch
    {
        OverseaThirdPartyKind.Google => "332303543001-mt3n63m59a8o33vs496a55ct6l42vipc.apps.googleusercontent.com",
        OverseaThirdPartyKind.Apple => "com.hoyoverse.platoversealogin",
        OverseaThirdPartyKind.Facebook => "2099441543493930",
        OverseaThirdPartyKind.Twitter => "R1liQ2o1TE8xWW43MUJaRFZzenE6MTpjaQ",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
    
    public static string BuildLoginUrl(OverseaThirdPartyKind kind, string languageCode)
    {
        var query = new StringBuilder(
            $"?client_id={GetClientId(kind)}" +
            $"&route={kind.ToString().ToLowerInvariant()}" +
            "&callback_method=deeplink" +
            $"&message_id={Guid.NewGuid()}" +
            $"&lang={languageCode}" +
            "&scheme=about%3Ablank");

        switch (kind)
        {
            case OverseaThirdPartyKind.Google:
                query.Append("&scope=email profile openid");
                query.Append("&response_type=id_token token");
                break;
            case OverseaThirdPartyKind.Apple:
                query.Append("&response_mode=fragment");
                query.Append("&response_id=code id_token");
                break;
            case OverseaThirdPartyKind.Facebook:
                query.Append("&scope=email");
                query.Append("&response_type=token");
                break;
            case OverseaThirdPartyKind.Twitter:
                query.Append("&scope=users.read tweet.read");
                break;
        }

        return $"{ApiEndpoints.OverseaThirdPartyOAuthUrl}{query}";
    }
}
