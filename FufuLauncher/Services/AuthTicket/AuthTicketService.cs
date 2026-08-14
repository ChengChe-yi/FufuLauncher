/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Services.MiHoYo.Passport;

namespace FufuLauncher.Services.AuthTicket;

public sealed class AuthTicketService : IAuthTicketService
{
    private readonly AccountManager _accountManager;

    public AuthTicketService(AccountManager accountManager)
    {
        _accountManager = accountManager;
    }

    public async Task<AuthTicketResult> CreateAuthTicketAsync(string accountId)
    {
        var result = new AuthTicketResult();

        try
        {
            var cookies = await _accountManager.LoadCookiesAsync(accountId);
            if (cookies == null || cookies.Count == 0)
            {
                result.ErrorMessage = "账号未登录";
                return result;
            }

            cookies.TryGetValue("stoken", out var stoken);
            cookies.TryGetValue("mid", out var mid);

            string stuid = GetFirstNonEmpty(cookies, "stuid", "account_id", "ltuid_v2", "account_id_v2");

            if (string.IsNullOrEmpty(stoken))
            {
                result.ErrorMessage = "账号登录状态已失效";
                return result;
            }
            if (string.IsNullOrEmpty(mid))
            {
                result.ErrorMessage = "账号登录状态已失效";
                return result;
            }

            bool isOversea = accountId.StartsWith("os", StringComparison.OrdinalIgnoreCase);

            string url;
            string bodyJson;

            if (isOversea)
            {
                url = ApiEndpoints.CreateAuthTicketBySTokenUrl;
                var request = new AuthTicketRequestOversea
                {
                    BizName = "hk4e_global",
                    Mid = mid,
                    SToken = stoken
                };
                bodyJson = JsonSerializer.Serialize(request);
            }
            else
            {
                url = ApiEndpoints.CreateAuthTicketByGameBizUrl;

                if (string.IsNullOrEmpty(stuid) || !int.TryParse(stuid, CultureInfo.InvariantCulture, out var uid))
                {
                    result.ErrorMessage = "账号登录状态已失效";
                    return result;
                }

                var request = new AuthTicketRequest
                {
                    GameBiz = "hk4e_cn",
                    Mid = mid,
                    SToken = stoken,
                    Uid = uid
                };
                bodyJson = JsonSerializer.Serialize(request);
            }

            using var httpClient = new HttpClient();
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

            // app_id=ddxf5dufpuyo,client_type=3,UA=HYPContainer
            ApplyHoyoPlayHeaders(httpRequest, cookies, isOversea);

            var response = await httpClient.SendAsync(httpRequest);
            var responseBody = await response.Content.ReadAsStringAsync();

            Debug.WriteLine($"[AuthTicket] 响应: {responseBody}");

            if (!response.IsSuccessStatusCode)
            {
                result.ErrorMessage = $"请求失败 (HTTP {(int)response.StatusCode})";
                return result;
            }

            var apiResponse = JsonSerializer.Deserialize<MihoyoApiResponse<AuthTicketData>>(responseBody);
            if (apiResponse == null)
            {
                result.ErrorMessage = "响应解析失败";
                return result;
            }

            if (apiResponse.RetCode != 0)
            {
                result.ErrorMessage = $"API 错误: [{apiResponse.RetCode}] {apiResponse.Message}";
                return result;
            }

            if (apiResponse.Data == null || string.IsNullOrEmpty(apiResponse.Data.Ticket))
            {
                result.ErrorMessage = "未获取到有效的凭证";
                return result;
            }

            result.Success = true;
            result.Ticket = apiResponse.Data.Ticket;
            Debug.WriteLine($"[AuthTicket] 成功获取 ticket (长度: {result.Ticket.Length})");
            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AuthTicket] 异常: {ex.Message}");
            result.ErrorMessage = $"请求异常: {ex.Message}";
            return result;
        }
    }
    
    private static void ApplyHoyoPlayHeaders(
        HttpRequestMessage request,
        IReadOnlyDictionary<string, string> cookies,
        bool isOversea)
    {
        request.Headers.UserAgent.ParseAdd(UserAgents.HoyoPlay);
        request.Headers.Accept.ParseAdd("application/json");

        if (isOversea)
        {
            request.Headers.Add(HeaderNames.RpcAppId, AppIds.HoyoPlayOversea);
            request.Headers.Add(HeaderNames.RpcClientType, "3");
            request.Headers.Add(HeaderNames.RpcDeviceId, PassportDeviceId.Generate53());
        }
        else
        {
            request.Headers.Add(HeaderNames.RpcAppId, AppIds.GameCombo);
            request.Headers.Add(HeaderNames.RpcClientType, "3");

            string cookieStr = BuildLTokenCookie(cookies);
            if (!string.IsNullOrEmpty(cookieStr))
                request.Headers.Add(HeaderNames.Cookie, cookieStr);
        }
    }
    
    private static string BuildLTokenCookie(IReadOnlyDictionary<string, string> cookies)
    {
        var pairs = new List<string>();
        if (TryFirstPair(cookies, "account_id", "account_id_v2") is { } aid)
            pairs.Add($"{aid.Key}={aid.Value}");
        if (TryFirstPair(cookies, "cookie_token", "cookie_token_v2") is { } ct)
            pairs.Add($"{ct.Key}={ct.Value}");
        if (TryFirstPair(cookies, "ltoken", "ltoken_v2") is { } lt)
            pairs.Add($"{lt.Key}={lt.Value}");
        if (TryFirstPair(cookies, "ltuid", "ltuid_v2") is { } lu)
            pairs.Add($"{lu.Key}={lu.Value}");
        return string.Join(";", pairs);
    }

    private static (string Key, string Value)? TryFirstPair(IReadOnlyDictionary<string, string> dict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dict.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                return (key, value);
        }
        return null;
    }

    private static string GetFirstNonEmpty(IReadOnlyDictionary<string, string> dict, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (dict.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
                return value;
        }
        return string.Empty;
    }
}