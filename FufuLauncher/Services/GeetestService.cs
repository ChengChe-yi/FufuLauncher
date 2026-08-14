/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Helpers;
using FufuLauncher.Models.MiHoYo.Identity;
using FufuLauncher.Models.MiHoYo.Passport;
using FufuLauncher.Services.MiHoYo;
using FufuLauncher.Services.MiHoYo.Transport;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace FufuLauncher.Services;

public sealed class GeetestService
{
    private const string CreateVerificationUrl = "https://api-takumi-record.mihoyo.com/game_record/app/card/wapi/createVerification?is_high=true";
    private const string VerifyVerificationUrl = "https://api-takumi-record.mihoyo.com/game_record/app/card/wapi/verifyVerification";
    private const string DailyNoteChallengePath = "/game_record/app/genshin/api/dailyNote";

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    public GeetestService()
    {
    }

    public async Task<string> TryVerifyForDailyNoteAsync(AccountContext ctx)
    {
        string createJson = await CallCreateVerificationAsync(ctx);
        string gt = null;
        string challenge = null;

        using (JsonDocument doc = JsonDocument.Parse(createJson))
        {
            int retcode = doc.RootElement.TryGetProperty("retcode", out JsonElement rc) ? rc.GetInt32() : -1;
            if (retcode != 0 || !doc.RootElement.TryGetProperty("data", out JsonElement data))
            {
                Debug.WriteLine($"[GeetestService] TryVerifyForDailyNote: createVerification 失败 retcode={retcode}");
                return null;
            }

            gt = data.TryGetProperty("gt", out JsonElement gtProp) ? gtProp.GetString() : null;
            challenge = data.TryGetProperty("challenge", out JsonElement chProp) ? chProp.GetString() : null;
            Debug.WriteLine($"[GeetestService] TryVerifyForDailyNote: createVerification 成功 gt={gt}, challenge={challenge}");
        }

        if (string.IsNullOrEmpty(gt) || string.IsNullOrEmpty(challenge))
            return null;

        GeetestResult result = await ShowGeetestWebViewAsync(gt, challenge, isOversea: false);
        if (result == null || string.IsNullOrEmpty(result.Validate))
        {
            Debug.WriteLine($"[GeetestService] TryVerifyForDailyNote: 用户未完成验证 (result=null) 或 validate 为空");
            return null;
        }
        Debug.WriteLine($"[GeetestService] TryVerifyForDailyNote: 验证码完成 challenge={result.Challenge}, validate={result.Validate}");

        string verifyJson = await CallVerifyVerificationAsync(ctx, result.Challenge, result.Validate);
        using (JsonDocument doc = JsonDocument.Parse(verifyJson))
        {
            int retcode = doc.RootElement.TryGetProperty("retcode", out JsonElement rc) ? rc.GetInt32() : -1;
            if (retcode != 0 || !doc.RootElement.TryGetProperty("data", out JsonElement data))
            {
                Debug.WriteLine($"[GeetestService] TryVerifyForDailyNote: verifyVerification 失败 retcode={retcode}");
                return null;
            }

            string finalChallenge = data.TryGetProperty("challenge", out JsonElement chProp) ? chProp.GetString() : null;
            Debug.WriteLine($"[GeetestService] TryVerifyForDailyNote: verifyVerification 成功 xrpc_challenge={finalChallenge}");
            return finalChallenge;
        }
    }

    private async Task<string> CallCreateVerificationAsync(AccountContext ctx)
    {
        string cookieStr = BbsRequestBuilder.BuildCookieString(ctx.Cookies, BbsRequestBuilder.CookieMode.Cookie);
        string ds = DailyNoteService.CalculateDS2(HeaderSalts.CnX4, "is_high=true", "");
        Debug.WriteLine($"[GeetestService] CallCreateVerification: device_fp={ctx.Device.DeviceFp}");

        using HttpRequestMessage req = new(HttpMethod.Get, CreateVerificationUrl);
        req.Headers.Add("Cookie", cookieStr);
        req.Headers.Add("x-rpc-app_version", HeaderVersions.MobileCnLogin);
        req.Headers.Add("x-rpc-client_type", "5");
        req.Headers.Add("x-rpc-device_id", ctx.Device.BbsDeviceId);
        req.Headers.Add("x-rpc-device_fp", ctx.Device.DeviceFp);
        req.Headers.Add("x-rpc-device_name", Uri.EscapeDataString(ctx.Device.DeviceName));
        req.Headers.Add("x-rpc-sys_version", ctx.Device.SysVersion);
        req.Headers.Add("x-rpc-challenge_game", "2");
        req.Headers.Add("x-rpc-challenge_path", DailyNoteChallengePath);
        req.Headers.Add("DS", ds);
        req.Headers.Add("Referer", UserAgents.WebstaticReferer);
        req.Headers.UserAgent.ParseAdd(ctx.UserAgent.Mobile);

        using HttpResponseMessage resp = await _httpClient.SendAsync(req);
        return await resp.Content.ReadAsStringAsync();
    }

    private async Task<string> CallVerifyVerificationAsync(AccountContext ctx, string challenge, string validate)
    {
        string cookieStr = BbsRequestBuilder.BuildCookieString(ctx.Cookies, BbsRequestBuilder.CookieMode.Cookie);
        GeetestWebResponse body = new()
        {
            Challenge = challenge,
            Validate = validate,
            Seccode = $"{validate}|jordan"
        };
        string bodyJson = JsonSerializer.Serialize(body);
        string ds = DailyNoteService.CalculateDS2(HeaderSalts.CnX4, "", bodyJson);
        Debug.WriteLine($"[GeetestService] CallVerifyVerification: device_fp={ctx.Device.DeviceFp}");

        using HttpRequestMessage req = new(HttpMethod.Post, VerifyVerificationUrl);
        req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        req.Headers.Add("Cookie", cookieStr);
        req.Headers.Add("x-rpc-app_version", HeaderVersions.MobileCnLogin);
        req.Headers.Add("x-rpc-client_type", "5");
        req.Headers.Add("x-rpc-device_id", ctx.Device.BbsDeviceId);
        req.Headers.Add("x-rpc-device_fp", ctx.Device.DeviceFp);
        req.Headers.Add("x-rpc-device_name", Uri.EscapeDataString(ctx.Device.DeviceName));
        req.Headers.Add("x-rpc-sys_version", ctx.Device.SysVersion);
        req.Headers.Add("x-rpc-challenge_game", "2");
        req.Headers.Add("x-rpc-challenge_path", DailyNoteChallengePath);
        req.Headers.Add("DS", ds);
        req.Headers.Add("Referer", UserAgents.WebstaticReferer);
        req.Headers.UserAgent.ParseAdd(ctx.UserAgent.Mobile);

        using HttpResponseMessage resp = await _httpClient.SendAsync(req);
        return await resp.Content.ReadAsStringAsync();
    }

    private static async Task<GeetestResult> ShowGeetestWebViewAsync(string gt, string challenge, bool isOversea, string? apiServerOverride = null)
    {
        string apiServer = apiServerOverride ?? (isOversea ? "api-na.geetest.com" : "api.geetest.com");
        TaskCompletionSource<GeetestResult> tcs = new();

        if (App.MainWindow == null)
        {
            Debug.WriteLine("[GeetestService] App.MainWindow 为 null，无法显示验证码窗口");
            tcs.TrySetResult(null);
            return await tcs.Task;
        }

        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                Window geetestWindow = new();
                geetestWindow.SystemBackdrop = new MicaBackdrop();
                geetestWindow.Title = "Geetest_CaptchaTitle".GetLocalized();

                Grid rootGrid = new() { Background = new SolidColorBrush(Colors.Transparent) };
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                Grid titleBar = new() { Height = 32 };
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Image icon = new()
                {
                    Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/WindowIcon.ico")),
                    Height = 16,
                    Width = 16,
                    Margin = new Thickness(16, 0, 12, 0)
                };
                Grid.SetColumn(icon, 0);
                titleBar.Children.Add(icon);

                TextBlock titleText = new()
                {
                    Text = "Geetest_CaptchaTitle".GetLocalized(),
                    VerticalAlignment = VerticalAlignment.Center,
                    Style = (Style)Application.Current.Resources["CaptionTextBlockStyle"]
                };
                Grid.SetColumn(titleText, 1);
                titleBar.Children.Add(titleText);

                Grid.SetRow(titleBar, 0);
                rootGrid.Children.Add(titleBar);

                WebView2 webView = new()
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetRow(webView, 1);
                rootGrid.Children.Add(webView);

                geetestWindow.Content = rootGrid;
                
                AppWindow appWindow = geetestWindow.AppWindow;
                if (appWindow != null)
                {
                    appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    appWindow.TitleBar.ButtonBackgroundColor = Colors.Transparent;
                    appWindow.TitleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
                    appWindow.Resize(new SizeInt32(1270, 720));

                    AppWindow mainAppWindow = App.MainWindow.AppWindow;
                    if (mainAppWindow != null)
                    {
                        PointInt32 mainPos = mainAppWindow.Position;
                        SizeInt32 mainSize = mainAppWindow.Size;
                        appWindow.Move(new PointInt32(
                            mainPos.X + (mainSize.Width - 400) / 2,
                            mainPos.Y + (mainSize.Height - 450) / 2));
                    }
                }

                geetestWindow.SetTitleBar(titleBar);

                await webView.EnsureCoreWebView2Async();
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    try
                    {
                        string msg = e.WebMessageAsJson;
                        GeetestResult result = JsonSerializer.Deserialize<GeetestResult>(msg);
                        tcs.TrySetResult(result);
                        geetestWindow.Close();
                    }
                    catch
                    {
                        tcs.TrySetResult(null);
                    }
                };

                geetestWindow.Closed += (s, e) =>
                {
                    tcs.TrySetResult(null);
                };

                string html = GetGeetestHtml(gt, challenge, apiServer);
                webView.NavigateToString(html);
                geetestWindow.Activate();
                try
                {
                    geetestWindow.AppWindow.MoveInZOrderAtTop();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GeetestService] MoveInZOrderAtTop 失败: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GeetestService] 验证码窗口创建失败: {ex.Message}");
                tcs.TrySetResult(null);
            }
        });

        return await tcs.Task;
    }

    private static string GetGeetestHtml(string gt, string challenge, string apiServer)
    {
        var captchaTitle = "Geetest_CaptchaTitle".GetLocalized();
        return $$"""
            <html>
                <head>
                    <meta charset="utf-8"/>
                    <title>{{captchaTitle}}</title>
                    <style>
                        * { margin:0; padding:0; box-sizing:border-box; }
                        body {
                            background: transparent;
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            height: 100vh;
                            font-family: 'Segoe UI', sans-serif;
                        }
                        #geetest-div { }
                    </style>
                </head>
                <body>
                    <div id="geetest-div"></div>
                </body>
                <script src="https://static.geetest.com/static/js/gt.0.5.2.js"></script>
                <script>
                    initGeetest(
                        {
                            protocol: "https://",
                            gt: "{{gt}}",
                            challenge: "{{challenge}}",
                            new_captcha: true,
                            product: 'bind',
                            api_server: '{{apiServer}}'
                        },
                        function (captchaObj) {
                            captchaObj.onReady(function () {
                                captchaObj.verify();
                            });
                            captchaObj.onSuccess(function () {
                                var result = captchaObj.getValidate();
                                chrome.webview.postMessage(result);
                            });
                            captchaObj.onError(function () {
                                chrome.webview.postMessage('{"geetest_error":1}');
                            });
                        }
                    );
                </script>
            </html>
            """;
    }
    
    public async Task<bool> TryVerifyAigisSessionAsync(IAigisProvider provider, string? rawSession, bool isOversea)
    {
        if (string.IsNullOrEmpty(rawSession))
        {
            return false;
        }

        Debug.WriteLine($"[GeetestService] TryVerifyAigisSession: rawSession={Truncate(rawSession, 500)}");

        AigisSession? session = Deserialize<AigisSession>(rawSession);
        if (session is null || string.IsNullOrEmpty(session.SessionId))
        {
            Debug.WriteLine("[GeetestService] TryVerifyAigisSession: Aigis 会话解析失败");
            return false;
        }

        GeetestVerification? verification = session.Data.ValueKind switch
        {
            JsonValueKind.String => Deserialize<GeetestVerification>(session.Data.GetString() ?? string.Empty),
            JsonValueKind.Object => DeserializeElement<GeetestVerification>(session.Data),
            _ => null
        };
        if (verification is null || string.IsNullOrEmpty(verification.Gt) || string.IsNullOrEmpty(verification.Challenge))
        {
            Debug.WriteLine($"[GeetestService] TryVerifyAigisSession: 极验参数解析失败 data={Truncate(session.Data.ToString(), 500)}");
            return false;
        }

        Debug.WriteLine($"[GeetestService] TryVerifyAigisSession: gt={Truncate(verification.Gt, 32)}, challenge={Truncate(verification.Challenge, 32)}, isOversea={isOversea}");

        GeetestResult? result = await ShowGeetestWebViewAsync(verification.Gt, verification.Challenge, isOversea);
        
        if (isOversea && (result is null || (result.Error == 1 && string.IsNullOrEmpty(result.Validate))))
        {
            Debug.WriteLine("[GeetestService] TryVerifyAigisSession: api-na 加载失败，回退 api.geetest.com 重试");
            result = await ShowGeetestWebViewAsync(verification.Gt, verification.Challenge, isOversea, apiServerOverride: "api.geetest.com");
        }

        if (result is null || string.IsNullOrEmpty(result.Validate))
        {
            Debug.WriteLine("[GeetestService] TryVerifyAigisSession: 极验未完成");
            return false;
        }

        var webResponse = new GeetestWebResponse
        {
            Challenge = result.Challenge,
            Validate = result.Validate,
            Seccode = string.IsNullOrEmpty(result.Seccode) ? $"{result.Validate}|jordan" : result.Seccode,
        };

        provider.Aigis = $"{session.SessionId};{Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(webResponse))}";
        return true;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static T? DeserializeElement<T>(JsonElement element)
    {
        try
        {
            return element.Deserialize<T>();
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private sealed class GeetestWebResponse
    {
        [JsonPropertyName("geetest_challenge")]
        public string Challenge
        {
            get; set;
        }

        [JsonPropertyName("geetest_validate")]
        public string Validate
        {
            get; set;
        }

        [JsonPropertyName("geetest_seccode")]
        public string Seccode
        {
            get; set;
        }
    }
}

public sealed class GeetestResult
{
    [JsonPropertyName("geetest_challenge")]
    public string Challenge
    {
        get; set;
    }

    [JsonPropertyName("geetest_validate")]
    public string Validate
    {
        get; set;
    }

    [JsonPropertyName("geetest_seccode")]
    public string Seccode
    {
        get; set;
    }
    
    [JsonPropertyName("geetest_error")]
    public int Error
    {
        get; set;
    }
}