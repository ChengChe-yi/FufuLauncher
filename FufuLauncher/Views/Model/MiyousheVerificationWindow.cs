using System.Text.Json;
using FufuLauncher.Helpers;
using FufuLauncher.Models.Miyoushe;
using FufuLauncher.Services.Miyoushe;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

internal sealed class MiyousheVerificationWindow : Window
{
    private readonly WebView2 _web = new();
    private readonly TaskCompletionSource<JsonElement?> _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly JsonElement _parameters;
    private readonly string? _session;
    private readonly string _nonce = Guid.NewGuid().ToString("N");
    private readonly CancellationToken _cancellation;
    private CancellationTokenRegistration _registration;
    private string _documentUri = "";

    private MiyousheVerificationWindow(JsonElement parameters, string? session, CancellationToken ct)
    {
        _parameters = parameters; _session = session; _cancellation = ct;
        Title = "Miyoushe_Verify".GetLocalized();
        SystemBackdrop = new MicaBackdrop();
        var root = new Grid();
        root.RowDefinitions.Add(new() { Height = GridLength.Auto });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        var hint = new TextBlock { Text = "Miyoushe_VerifyHint".GetLocalized(), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(20, 16, 20, 12) };
        root.Children.Add(hint);
        Grid.SetRow(_web, 1); root.Children.Add(_web); Content = root;
        WindowManagerHelper.ResizeWithDpi(AppWindow, this, 480, 550);
        WindowManagerHelper.CenterWindowOnScreen(AppWindow, 480, 550);
        _web.Loaded += OnLoaded;
        Closed += (_, _) => { _registration.Dispose(); _result.TrySetResult(null); _web.Close(); };
    }

    public static async Task<JsonElement?> ShowAsync(JsonElement parameters, string? session, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var window = new MiyousheVerificationWindow(parameters.Clone(), session, ct);
        window.Activate();
        window._registration = ct.Register(() => window.DispatcherQueue.TryEnqueue(() => window.Close()));
        return await window._result.Task;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        try
        {
            await _web.EnsureCoreWebView2Async();
            if (_cancellation.IsCancellationRequested) return;
            var core = _web.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.NavigationStarting += (_, e) => { if (e.Uri != _documentUri) e.Cancel = true; };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = Microsoft.Web.WebView2.Core.CoreWebView2PermissionState.Deny;
            core.WebMessageReceived += (_, e) =>
            {
                if (e.Source != _documentUri) return;
                try
                {
                    using var message = JsonDocument.Parse(e.WebMessageAsJson);
                    if (message.RootElement.Text("nonce") != _nonce) return;
                    if (message.RootElement.Flag("cancelled")) { _result.TrySetResult(null); Close(); return; }
                    var result = message.RootElement.Get("result");
                    bool v4 = _parameters.Flag("use_v4") || _parameters.Text("challenge").Length == 0;
                    if (result.ValueKind == JsonValueKind.Object && (v4
                        ? result.Text("captcha_id").Length > 0 && result.Text("lot_number").Length > 0 && result.Text("pass_token").Length > 0
                        : result.Text("geetest_challenge").Length > 0 && result.Text("geetest_validate").Length > 0 && result.Text("geetest_seccode").Length > 0))
                    { _result.TrySetResult(result.Clone()); Close(); }
                }
                catch (JsonException) { }
            };
            bool useV4 = _parameters.Flag("use_v4") || _parameters.Text("challenge").Length == 0;
            var config = useV4 ? JsonSerializer.Serialize(new
            {
                captchaId = _parameters.Text("gt"), riskType = _parameters.Text("risk_type"), product = "popup",
                nextWidth = "300px", lang = "zho", userInfo = JsonSerializer.Serialize(new { session_id = _session }), https = true, protocol = "https"
            }) : JsonSerializer.Serialize(new
            {
                gt = _parameters.Text("gt"), challenge = _parameters.Text("challenge"), offline = false, new_captcha = true,
                product = "embed", width = "300px", https = true
            });
            _documentUri = CommunityContent.ToDocumentUri($$"""
                <!doctype html><html><head><meta charset="utf-8"><meta name="referrer" content="no-referrer">
                <style>body{margin:0;padding:24px;background:#f7f7f7;color:#333;font:14px 'Segoe UI','Microsoft YaHei UI',sans-serif;text-align:center}#captcha{display:flex;justify-content:center}#error{margin:20px;color:#a13030}</style></head>
                <body><div id="captcha"></div><p id="error"></p><script>
                const fail=()=>document.getElementById('error').textContent={{JsonSerializer.Serialize("Miyoushe_VerifyLoadError".GetLocalized())}};
                const sdk=document.createElement('script');sdk.src='{{(useV4 ? "https://static.geetest.com/v4/gt4.js" : "https://static.geetest.com/static/js/gt.0.4.9.js")}}';sdk.onerror=fail;
                sdk.onload=()=>{try{ {{(useV4 ? "initGeetest4" : "initGeetest")}}({{config}},c=>{
                    c.appendTo('#captcha');c.onError(fail);c.onSuccess(()=>{
                        const result=c.getValidate();if(result)window.chrome.webview.postMessage({nonce:{{JsonSerializer.Serialize(_nonce)}},result});
                    });
                    c.onClose(()=>window.chrome.webview.postMessage({nonce:{{JsonSerializer.Serialize(_nonce)}},cancelled:true}));
                }); } catch(e){fail()} };document.head.appendChild(sdk);
                setTimeout(()=>{if(!document.querySelector('#captcha').children.length)fail()},25000);
                </script></body></html>
                """);
            core.Navigate(_documentUri);
        }
        catch (Exception)
        {
            _result.TrySetException(new InvalidOperationException("Miyoushe_VerifyLoadError".GetLocalized()));
            Close();
        }
    }
}
