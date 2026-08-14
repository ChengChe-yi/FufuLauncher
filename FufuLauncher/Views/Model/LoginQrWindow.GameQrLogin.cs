/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using FufuLauncher.Constants;

namespace FufuLauncher.Views;

public sealed partial class LoginQrWindow
{
    #region 游戏扫码登录
    private async Task StartGameLoginFlowAsync(LoginSession session)
    {
        _isLoginCompleting = false;
        UpdateStatus("正在创建游戏扫码二维码...", true);

        var qrResult = await CreateGameQrCodeAsync(session);
        if (!qrResult.Success)
        {
            UpdateStatus($"创建失败: {qrResult.Message}", false);
            return;
        }

        RenderQrCode(qrResult.Url);
        UpdateStatus("请使用米游社或对应游戏内扫描二维码", false, true);

        await PollGameLoginStatusAsync(session);
    }

    private async Task<(bool Success, string Url, string Message)> CreateGameQrCodeAsync(LoginSession session)
    {
        string url = ApiEndpoints.Hk4eQrCodeFetchUrl;
        var requestBody = new JsonObject
        {
            ["app_id"] = int.Parse(session.GameAppId),
            ["device"] = session.GameDevice
        };
        string bodyStr = requestBody.ToJsonString(_jsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
        AddGameHeaders(request, bodyStr, "");

        try
        {
            var response = await _httpClient.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            var result = JsonNode.Parse(responseStr);

            if (result["retcode"]?.GetValue<int>() == 0)
            {
                string qrUrl = result["data"]["url"]?.GetValue<string>();
                var uri = new Uri(qrUrl);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                session.Ticket = query["ticket"];  // 存入 session
                return (true, qrUrl, "Success");
            }
            return (false, null, result["message"]?.GetValue<string>());
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private async Task PollGameLoginStatusAsync(LoginSession session)
    {
        const int MaxConsecutiveErrors = 5;
        const int TimeoutSeconds = 180;
        int consecutiveErrors = 0;
        var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(session.Cts.Token, timeoutCts.Token);
        CancellationToken ct = linkedCts.Token;

        string url = ApiEndpoints.Hk4eQrCodeQueryUrl;
        int pollInterval = 3000;

        while (!ct.IsCancellationRequested)
        {
            var requestBody = new JsonObject
            {
                ["app_id"] = int.Parse(session.GameAppId),
                ["device"] = session.GameDevice,
                ["ticket"] = session.Ticket
            };
            string bodyStr = requestBody.ToJsonString(_jsonOptions);

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            AddGameHeaders(request, bodyStr, "");

            try
            {
                var response = await _httpClient.SendAsync(request, ct);
                string responseStr = await response.Content.ReadAsStringAsync();
                var result = JsonNode.Parse(responseStr);

                int retcode = result["retcode"]?.GetValue<int>() ?? -1;

                if (retcode == 0)
                {
                    string stat = result["data"]["stat"]?.GetValue<string>();
                    if (stat == "Confirmed")
                    {
                        _isLoginCompleting = true;
                        UpdateStatus("扫码成功，正在换取SToken...", true);
                        string raw = result["data"]["payload"]?["raw"]?.GetValue<string>();
                        if (!string.IsNullOrEmpty(raw))
                        {
                            var rawNode = JsonNode.Parse(raw);
                            string uid = rawNode["uid"]?.GetValue<string>();
                            string token = rawNode["token"]?.GetValue<string>();
                            await GetSTokenByGameTokenAsync(uid, token);
                            return;
                        }
                    }
                    else if (stat == "Scanned")
                    {
                        UpdateStatus("已扫码，请在手机端确认登录...", true);
                    }
                    consecutiveErrors = 0;
                }
                else
                {
                    consecutiveErrors++;
                    Debug.WriteLine($"游戏二维码轮询异常码: {retcode}");
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                    UpdateStatus("登录超时，请重新获取二维码", false);
                return;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                Debug.WriteLine($"游戏轮询异常: {ex.Message}");
            }

            if (consecutiveErrors >= MaxConsecutiveErrors)
            {
                UpdateStatus("网络异常次数过多，请检查网络后重试", false);
                return;
            }

            await Task.Delay(pollInterval, ct);
        }
    }

    private async Task GetSTokenByGameTokenAsync(string accountId, string gameToken)
    {
        string url = ApiEndpoints.GetTokenByGameTokenUrl;
        var requestBody = new JsonObject
        {
            ["account_id"] = int.Parse(accountId),
            ["game_token"] = gameToken
        };
        string bodyStr = requestBody.ToJsonString(_jsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");

        request.Headers.TryAddWithoutValidation("x-rpc-app_version", "2.71.1");
        request.Headers.TryAddWithoutValidation("x-rpc-game_biz", "bbs_cn");
        request.Headers.TryAddWithoutValidation("x-rpc-sys_version", "12");
        request.Headers.TryAddWithoutValidation("x-rpc-device_id", _deviceId);
        request.Headers.TryAddWithoutValidation("x-rpc-device_name", "Xiaomi MI 6");
        request.Headers.TryAddWithoutValidation("x-rpc-device_model", "MI 6");
        request.Headers.TryAddWithoutValidation("x-rpc-app_id", "bll8iq97cem8");
        request.Headers.TryAddWithoutValidation("x-rpc-client_type", "4");
        request.Headers.TryAddWithoutValidation("User-Agent", "okhttp/4.9.3");
        request.Headers.TryAddWithoutValidation("DS", GenerateGameDS2(bodyStr, ""));

        try
        {
            var response = await _httpClient.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            var result = JsonNode.Parse(responseStr);

            if (result["retcode"]?.GetValue<int>() == 0)
            {
                string stoken = result["data"]["token"]?["token"]?.GetValue<string>();
                string mid = result["data"]["user_info"]?["mid"]?.GetValue<string>();

                if (!string.IsNullOrEmpty(stoken) && !string.IsNullOrEmpty(mid))
                {
                    var cookies = await ExchangeV2TokensAsync(stoken, mid, accountId);
                    if (cookies != null)
                    {
                        OnLoginSuccess(cookies, "cn");
                    }
                    return;
                }
            }
            UpdateStatus($"SToken换取失败: {result["message"]?.GetValue<string>()}", false);
        }
        catch (Exception ex)
        {
            UpdateStatus($"SToken换取异常: {ex.Message}", false);
        }
    }

    private void AddGameHeaders(HttpRequestMessage request, string body, string query)
    {
        request.Headers.TryAddWithoutValidation("x-rpc-app_version", "2.71.1");
        request.Headers.TryAddWithoutValidation("x-rpc-aigis", "");
        request.Headers.TryAddWithoutValidation("Accept", "application/json");
        request.Headers.TryAddWithoutValidation("x-rpc-game_biz", "bbs_cn");
        request.Headers.TryAddWithoutValidation("x-rpc-sys_version", "12");
        request.Headers.TryAddWithoutValidation("x-rpc-device_id", _deviceId);
        request.Headers.TryAddWithoutValidation("x-rpc-device_name", "Xiaomi MI 6");
        request.Headers.TryAddWithoutValidation("x-rpc-device_model", "MI 6");
        request.Headers.TryAddWithoutValidation("x-rpc-client_type", "3");
        request.Headers.TryAddWithoutValidation("User-Agent", "okhttp/4.9.3");
    }

    private string GenerateGameDS2(string body, string query)
    {
        long t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        string r = new Random().Next(100001, 200000).ToString();
        string b = string.IsNullOrEmpty(body) ? "" : body;
        string q = string.IsNullOrEmpty(query) ? "" : query;
        string signStr = $"salt={SaltGame}&t={t}&r={r}&b={b}&q={q}";
        string sign = CreateMD5(signStr);
        return $"{t},{r},{sign}";
    }
    #endregion
}
