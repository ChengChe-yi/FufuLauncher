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
    #region 米游社APP扫码登录
    private async Task StartAppLoginFlowAsync(LoginSession session)
    {
        _isLoginCompleting = false;
        UpdateStatus("正在创建APP登录二维码...", true);

        var qrResult = await CreateAppQrCodeAsync(session);
        if (!qrResult.Success)
        {
            UpdateStatus($"创建失败: {qrResult.Message}", false);
            return;
        }

        RenderQrCode(qrResult.Url);
        UpdateStatus("请使用米游社APP扫描二维码", false, true);

        await PollAppLoginStatusAsync(session);
    }

    private async Task<(bool Success, string Url, string Message)> CreateAppQrCodeAsync(LoginSession session)
    {
        string url = ApiEndpoints.PassportAppCreateQrLoginUrl;
        var body = new JsonObject();
        string bodyStr = body.ToJsonString(_jsonOptions);

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
        AddCommonHeaders(request, bodyStr, "", "3", "ddxf5dufpuyo", "2.90.1");

        try
        {
            var response = await _httpClient.SendAsync(request);
            string responseStr = await response.Content.ReadAsStringAsync();
            var result = JsonNode.Parse(responseStr);

            if (result["retcode"]?.GetValue<int>() == 0)
            {
                string qrUrl = result["data"]["url"]?.GetValue<string>();
                session.Ticket = result["data"]["ticket"]?.GetValue<string>(); 
                return (true, qrUrl, "Success");
            }
            return (false, null, result["message"]?.GetValue<string>());
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    private async Task PollAppLoginStatusAsync(LoginSession session)
    {
        const int MaxConsecutiveErrors = 5;
        const int TimeoutSeconds = 180;
        int consecutiveErrors = 0;
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(session.Cts.Token, timeoutCts.Token);
        CancellationToken ct = linkedCts.Token;

        string url = ApiEndpoints.PassportAppQueryQrLoginStatusUrl;
        int pollInterval = 3000;
        JsonNode confirmedData = null;

        while (!ct.IsCancellationRequested)
        {
            var body = new JsonObject { ["ticket"] = session.Ticket };
            string bodyStr = body.ToJsonString(_jsonOptions);

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            AddCommonHeaders(request, bodyStr, "", "3", "ddxf5dufpuyo", "2.90.1");

            try
            {
                using var response = await _httpClient.SendAsync(request, ct);
                string responseStr = await response.Content.ReadAsStringAsync();
                var result = JsonNode.Parse(responseStr);

                int retcode = result?["retcode"]?.GetValue<int>() ?? -1;

                if (retcode == -3501 || retcode == -106)
                {
                    if (!_isLoginCompleting)
                        UpdateStatus("二维码已失效或过期", false);
                    return;
                }

                if (retcode == 0)
                {
                    string status = result["data"]["status"]?.GetValue<string>();
                    if (status?.ToLower() == "confirmed")
                    {
                        _isLoginCompleting = true;
                        UpdateStatus("APP扫码成功，正在换取...", true);
                        confirmedData = result["data"];
                        break;
                    }
                    if (status?.ToLower() == "scanned")
                    {
                        UpdateStatus("已扫码，请在手机端确认登录...", true);
                    }
                    consecutiveErrors = 0; 
                }
                else
                {
                    consecutiveErrors++;
                    Debug.WriteLine($"轮询返回非预期码: {retcode}");
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
                Debug.WriteLine($"轮询异常: {ex.Message}");
            }

            if (consecutiveErrors >= MaxConsecutiveErrors)
            {
                UpdateStatus("网络异常次数过多，请检查网络后重试", false);
                return;
            }

            try
            {
                await Task.Delay(pollInterval, ct);
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                    UpdateStatus("登录超时，请重新获取二维码", false);
                return;
            }
        }

        if (confirmedData != null)
        {
            await ProcessAndExchangeV2TokensAsync(confirmedData);
        }
    }
    #endregion
}
