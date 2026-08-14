/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text;
using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Models.MiHoYo.Passport;
using FufuLauncher.Services.MiHoYo.Networking;

namespace FufuLauncher.Services.MiHoYo.Passport;

public sealed class PassportClient
{
    private const string AigisHeader = "X-Rpc-Aigis";

    private static readonly HttpClient _httpClient = new(new HttpClientHandler { UseCookies = false })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private readonly string _deviceId = Guid.NewGuid().ToString();
    
    public async Task<(string? Aigis, PassportResponse<MobileCaptcha> Response)> CreateLoginCaptchaAsync(
        string mobile, string? aigis, CancellationToken token = default)
    {
        var data = new Dictionary<string, string>
        {
            ["area_code"] = PassportRsaCrypto.EncryptCn("+86"),
            ["mobile"] = PassportRsaCrypto.EncryptCn(mobile),
        };

        using var request = CreateCnJsonRequest(HttpMethod.Post, ApiEndpoints.AccountCreateLoginCaptchaUrl, data, aigis);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        PassportResponse<MobileCaptcha> body = await PassportHttpUtil.DeserializeAsync<MobileCaptcha>(response, token).ConfigureAwait(false);
        return (PassportHttpUtil.GetSingleHeader(response, AigisHeader), body);
    }
    
    public async Task<PassportResponse<LoginResult>> LoginByMobileCaptchaAsync(
        string actionType, string mobile, string captcha, string? aigis, CancellationToken token = default)
    {
        var data = new Dictionary<string, string>
        {
            ["area_code"] = PassportRsaCrypto.EncryptCn("+86"),
            ["action_type"] = actionType,
            ["captcha"] = captcha,
            ["mobile"] = PassportRsaCrypto.EncryptCn(mobile),
        };

        using var request = CreateCnJsonRequest(HttpMethod.Post, ApiEndpoints.AccountLoginByMobileCaptchaUrl, data, aigis);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        return await PassportHttpUtil.DeserializeAsync<LoginResult>(response, token).ConfigureAwait(false);
    }
    
    public async Task<string> GetLTokenBySTokenAsync(string stoken, string mid, string aid, CancellationToken token = default)
    {
        using var request = CreateCnAuthRequest(ApiEndpoints.GetLTokenBySTokenUrl, $"mid={mid}; stoken={stoken}; stuid={aid}");
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        PassportResponse<LTokenWrapper> body = await PassportHttpUtil.DeserializeAsync<LTokenWrapper>(response, token).ConfigureAwait(false);
        return body.IsSuccess ? body.Data?.LToken ?? string.Empty : string.Empty;
    }
    
    public async Task<string> GetCookieAccountInfoBySTokenAsync(string stoken, string mid, string aid, CancellationToken token = default)
    {
        using var request = CreateCnAuthRequest(ApiEndpoints.GetCookieAccountInfoBySTokenUrl, $"mid={mid}; stoken={stoken}; stuid={aid}");
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        PassportResponse<UidCookieToken> body = await PassportHttpUtil.DeserializeAsync<UidCookieToken>(response, token).ConfigureAwait(false);
        return body.IsSuccess ? body.Data?.CookieToken ?? string.Empty : string.Empty;
    }
    
    private HttpRequestMessage CreateCnJsonRequest(HttpMethod method, string url, Dictionary<string, string> data, string? aigis)
    {
        string body = JsonSerializer.Serialize(data);
        var request = new HttpRequestMessage(method, url);
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        ApplyCnHeaders(request, body, aigis, cookie: null);
        return request;
    }
    
    private HttpRequestMessage CreateCnAuthRequest(string url, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        ApplyCnHeaders(request, body: string.Empty, aigis: null, cookie);
        return request;
    }
    
    private void ApplyCnHeaders(HttpRequestMessage request, string body, string? aigis, string? cookie)
    {
        request.Headers.TryAddWithoutValidation(HeaderNames.UserAgent, UserAgents.WindowsBbs295);
        request.Headers.TryAddWithoutValidation(HeaderNames.Accept, "application/json");
        request.Headers.TryAddWithoutValidation("Accept-Language", "zh-cn");
        if (!string.IsNullOrEmpty(cookie))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Cookie, cookie);
        }

        request.Headers.TryAddWithoutValidation(HeaderNames.RpcAigis, aigis ?? string.Empty);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcAppId, AppIds.Passport);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcAppVersion, HeaderVersions.BbsCn295);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcClientType, ClientTypes.AndroidApp);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcDeviceId, _deviceId);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcDeviceName, string.Empty);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcGameBiz, GameBizValues.BbsCn);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcSdkVersion, HeaderVersions.PassportSdkVersion);
        request.Headers.TryAddWithoutValidation(HeaderNames.DS, MiHoYoHeaderFactory.CalculateDsGen2(HeaderSalts.PassportProd, body));
    }
}
