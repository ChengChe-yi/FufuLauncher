/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Models.MiHoYo.Passport;

namespace FufuLauncher.Services.MiHoYo.Passport;

public sealed class OverseaPassportClient
{
    private const string AigisHeader = "X-Rpc-Aigis";
    private const string VerifyHeader = "X-Rpc-Verify";

    private static readonly HttpClient _httpClient = new(new HttpClientHandler { UseCookies = false })
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    private readonly string _deviceId53 = PassportDeviceId.Generate53();
    private readonly string _deviceId36 = Guid.NewGuid().ToString();
    
    public async Task<(string? Aigis, string? Risk, PassportResponse<LoginResult> Response)> LoginByPasswordAsync(
        string account, string password, string? aigis, string? verify, CancellationToken token = default)
    {
        var data = new Dictionary<string, object>
        {
            ["account"] = PassportRsaCrypto.EncryptOversea(account),
            ["password"] = PassportRsaCrypto.EncryptOversea(password),
            ["token_type"] = 2,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoints.OverseaLoginByPasswordUrl);
        request.Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
        ApplyHoyoPlayHeaders(request, aigis, verify);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        PassportResponse<LoginResult> body = await PassportHttpUtil.DeserializeAsync<LoginResult>(response, token).ConfigureAwait(false);
        string? rpcAigis = PassportHttpUtil.GetSingleHeader(response, AigisHeader);
        string? rpcVerify = PassportHttpUtil.GetSingleHeader(response, VerifyHeader);
        Debug.WriteLine($"[OverseaPassport] LoginByPassword: HTTP={(int)response.StatusCode}, retcode={body.RetCode}, aigis={(rpcAigis is null ? "无" : Truncate(rpcAigis, 300))}, risk={(rpcVerify is null ? "无" : Truncate(rpcVerify, 300))}");
        return (rpcAigis, rpcVerify, body);
    }
    
    public async Task<(string? Risk, PassportResponse<LoginResult> Response)> LoginByThirdPartyAsync(
        ThirdPartyToken thirdPartyToken, string? verify, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiEndpoints.OverseaLoginByThirdPartyUrl);
        request.Content = new StringContent(JsonSerializer.Serialize(thirdPartyToken), Encoding.UTF8, "application/json");
        ApplyHoyoPlayHeaders(request, aigis: null, verify);

        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        PassportResponse<LoginResult> body = await PassportHttpUtil.DeserializeAsync<LoginResult>(response, token).ConfigureAwait(false);
        string? rpcVerify = PassportHttpUtil.GetSingleHeader(response, VerifyHeader);
        Debug.WriteLine($"[OverseaPassport] LoginByThirdParty({thirdPartyToken.ThirdPartyType}): HTTP={(int)response.StatusCode}, retcode={body.RetCode}, risk={(rpcVerify is null ? "无" : Truncate(rpcVerify, 300))}");
        return (rpcVerify, body);
    }
    
    public async Task<PassportResponse<ActionTicketInfo>> GetActionTicketInfoAsync(string ticket, CancellationToken token = default)
    {
        var data = new ActionTicketInfoRequest { ActionTicket = ticket };
        using var request = CreateMaVerifierRequest(ApiEndpoints.OverseaGetActionTicketInfoUrl, data, aigis: null);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        return await PassportHttpUtil.DeserializeAsync<ActionTicketInfo>(response, token).ConfigureAwait(false);
    }
    
    public async Task<(string? Aigis, PassportResponse Response)> CreateEmailCaptchaByActionTicketAsync(
        string ticket, string? aigis, CancellationToken token = default)
    {
        var data = new ActionTicketInfoRequest { ActionTicket = ticket };
        using var request = CreateMaVerifierRequest(ApiEndpoints.OverseaCreateEmailCaptchaByActionTicketUrl, data, aigis);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        PassportResponse body = await PassportHttpUtil.DeserializeAsync(response, token).ConfigureAwait(false);
        string? rpcAigis = PassportHttpUtil.GetSingleHeader(response, AigisHeader);
        Debug.WriteLine($"[OverseaPassport] CreateEmailCaptchaByActionTicket: HTTP={(int)response.StatusCode}, retcode={body.RetCode}, aigis={(rpcAigis is null ? "无" : Truncate(rpcAigis, 300))}");
        return (rpcAigis, body);
    }
    
    public async Task<PassportResponse> VerifyActionTicketPartlyAsync(string ticket, string captcha, CancellationToken token = default)
    {
        var data = new ActionTicketInfoRequest
        {
            ActionTicket = ticket,
            EmailCaptcha = captcha,
            VerifyMethod = 2,
        };
        using var request = CreateMaVerifierRequest(ApiEndpoints.OverseaVerifyActionTicketPartlyUrl, data, aigis: null);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        return await PassportHttpUtil.DeserializeAsync(response, token).ConfigureAwait(false);
    }
    
    public async Task<string> GetLTokenBySTokenAsync(string stoken, string mid, string aid, CancellationToken token = default)
    {
        using var request = CreateStokenExchangeRequest(ApiEndpoints.OverseaGetLTokenBySTokenUrl, stoken, mid, aid);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        PassportResponse<LTokenWrapper> body = await PassportHttpUtil.DeserializeAsync<LTokenWrapper>(response, token).ConfigureAwait(false);
        return body.IsSuccess ? body.Data?.LToken ?? string.Empty : string.Empty;
    }
    
    public async Task<string> GetCookieAccountInfoBySTokenAsync(string stoken, string mid, string aid, CancellationToken token = default)
    {
        using var request = CreateStokenExchangeRequest(ApiEndpoints.OverseaGetCookieAccountInfoBySTokenUrl, stoken, mid, aid);
        using HttpResponseMessage response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        PassportResponse<UidCookieToken> body = await PassportHttpUtil.DeserializeAsync<UidCookieToken>(response, token).ConfigureAwait(false);
        return body.IsSuccess ? body.Data?.CookieToken ?? string.Empty : string.Empty;
    }
    
    private HttpRequestMessage CreateMaVerifierRequest(string url, ActionTicketInfoRequest data, string? aigis)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(data), Encoding.UTF8, "application/json");
        ApplyOverseaBbsHeaders(request, aigis);
        return request;
    }
    
    private HttpRequestMessage CreateStokenExchangeRequest(string url, string stoken, string mid, string aid)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(JsonSerializer.Serialize(new STokenWrapper(stoken, aid)), Encoding.UTF8, "application/json");
        ApplyOverseaBbsHeaders(request, aigis: null);
        request.Headers.TryAddWithoutValidation(HeaderNames.Cookie, $"stoken={stoken}; mid={mid}; stuid={aid}");
        return request;
    }
    
    private void ApplyHoyoPlayHeaders(HttpRequestMessage request, string? aigis, string? verify)
    {
        request.Headers.TryAddWithoutValidation(HeaderNames.UserAgent, UserAgents.HoyoPlay);
        request.Headers.TryAddWithoutValidation(HeaderNames.Accept, "application/json");
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcAppId, AppIds.HoyoPlayOversea);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcClientType, ClientTypes.GameApp);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcDeviceId, _deviceId53);
        if (!string.IsNullOrEmpty(aigis))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.RpcAigis, aigis);
        }

        if (!string.IsNullOrEmpty(verify))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.RpcVerify, verify);
        }
    }

    private void ApplyOverseaBbsHeaders(HttpRequestMessage request, string? aigis)
    {
        request.Headers.TryAddWithoutValidation(HeaderNames.UserAgent, UserAgents.WindowsBbsOversea254);
        request.Headers.TryAddWithoutValidation(HeaderNames.Accept, "application/json");
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcAppVersion, HeaderVersions.BbsOs254);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcClientType, ClientTypes.Other);
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcLanguage, "zh-cn");
        request.Headers.TryAddWithoutValidation(HeaderNames.RpcDeviceId, _deviceId36);
        if (!string.IsNullOrEmpty(aigis))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.RpcAigis, aigis);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
