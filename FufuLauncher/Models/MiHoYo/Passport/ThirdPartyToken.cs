/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.MiHoYo.Passport;

public sealed class ThirdPartyToken : IVerifyProvider
{
    public ThirdPartyToken(string thirdPartyType, string token)
    {
        ThirdPartyType = thirdPartyType;
        Token = token;
    }
    
    [JsonPropertyName("thirdparty_type")]
    public string ThirdPartyType { get; set; }
    
    [JsonPropertyName("token")]
    public string Token { get; set; }
    
    [JsonIgnore]
    public string? Verify { get; set; }
}
