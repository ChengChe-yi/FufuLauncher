/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text.Json;
using FufuLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.ViewModels;

public partial class PluginStoreViewModel
{
    #region Shared UI Dialogs

    private static async Task<string?> ShowGeetestCaptchaAsync(string verifyUrl)
    {
        var tcs = new TaskCompletionSource<string?>();

        // Guard: ensure MainWindow and its DispatcherQueue are available
        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show captcha: MainWindow or DispatcherQueue is null");
            return null;
        }

        var enqueued = dispatcherQueue.TryEnqueue(async () =>
        {
            Window? captchaWindow = null;
            CancellationTokenSource? pollCts = null;
            try
            {
                captchaWindow = new Window();
                captchaWindow.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
                captchaWindow.Title = "人机验证";

                var rootGrid = new Grid();
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
                rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                
                var titleBar = new Grid { Height = 32 };
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var titleText = new TextBlock
                {
                    Text = "下载验证",
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(16, 0, 0, 0)
                };
                Grid.SetColumn(titleText, 1);
                titleBar.Children.Add(titleText);

                Grid.SetRow(titleBar, 0);
                rootGrid.Children.Add(titleBar);

                var webView = new WebView2
                {
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                Grid.SetRow(webView, 1);
                rootGrid.Children.Add(webView);

                captchaWindow.Content = rootGrid;
                
                // Configure AppWindow with null guard
                if (captchaWindow.AppWindow is { } appWindow)
                {
                    appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
                    appWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    appWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
                    appWindow.Resize(new Windows.Graphics.SizeInt32(1280, 720));

                    // Center on main window (if available)
                    if (App.MainWindow?.AppWindow is { } mainAppWindow)
                    {
                        var mainPos = mainAppWindow.Position;
                        var mainSize = mainAppWindow.Size;
                        appWindow.Move(new Windows.Graphics.PointInt32(
                            mainPos.X + (mainSize.Width - 1280) / 2,
                            mainPos.Y + (mainSize.Height - 720) / 2));
                    }
                }

                captchaWindow.SetTitleBar(titleBar);

                await webView.EnsureCoreWebView2Async();
                
                // Guard: CoreWebView2 must be non-null after initialization
                if (webView.CoreWebView2 is not { } coreWebView)
                {
                    Debug.WriteLine("[PluginStoreVM] CoreWebView2 is null after EnsureCoreWebView2Async");
                    tcs.TrySetResult(null);
                    captchaWindow.Close();
                    return;
                }

                coreWebView.Settings.AreDefaultContextMenusEnabled = false;
                coreWebView.Settings.IsStatusBarEnabled = false;

                pollCts = new CancellationTokenSource();
                var pollToken = pollCts.Token;
                
                coreWebView.NavigationCompleted += async (s, e) =>
                {
                    if (!e.IsSuccess) return;
                    Debug.WriteLine($"[PluginStoreVM] Gate page loaded, starting poll for dl_token...");

                    try
                    {
                        for (var i = 0; i < 120 && !pollToken.IsCancellationRequested; i++)
                        {
                            await Task.Delay(500, pollToken);

                            string raw;
                            try { raw = await webView.CoreWebView2.ExecuteScriptAsync("document.body.textContent"); }
                            catch { continue; }

                            if (string.IsNullOrWhiteSpace(raw)) continue;
                            
                            var unescaped = raw.Trim('"').Replace("\\\"", "\"").Replace("\\\\", "\\");

                            if (!unescaped.StartsWith("{")) continue;

                            try
                            {
                                using var doc = JsonDocument.Parse(unescaped);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("retcode", out var rc) && rc.GetInt32() == 0 &&
                                    root.TryGetProperty("data", out var data) &&
                                    data.TryGetProperty("dl_token", out var dlToken))
                                {
                                    var token = dlToken.GetString();
                                    if (!string.IsNullOrWhiteSpace(token))
                                    {
                                        Debug.WriteLine($"[PluginStoreVM] Got dl_token: {token[..12]}...");
                                        pollCts.Cancel();
                                        tcs.TrySetResult(token);
                                        captchaWindow.DispatcherQueue.TryEnqueue(() => captchaWindow.Close());
                                        return;
                                    }
                                }
                            }
                            catch (JsonException) { }
                        }
                    }
                    catch (TaskCanceledException) { }
                };

                captchaWindow.Closed += (_, _) =>
                {
                    pollCts?.Cancel();
                    tcs.TrySetResult(null);
                };

                Debug.WriteLine($"[PluginStoreVM] Navigating to captcha gate: {verifyUrl}");
                coreWebView.Navigate(verifyUrl);
                captchaWindow.Activate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error in captcha window: {ex}");
                tcs.TrySetResult(null);
                pollCts?.Cancel();
                // Best-effort close the window if it was created
                if (captchaWindow is not null)
                {
                    try { captchaWindow.DispatcherQueue.TryEnqueue(() => captchaWindow.Close()); }
                    catch { /* ignore cleanup failures */ }
                }
            }
        });

        if (!enqueued)
        {
            Debug.WriteLine("[PluginStoreVM] Failed to enqueue captcha window to DispatcherQueue");
            return null;
        }

        return await tcs.Task;
    }
    
    private static async Task<string?> ShowPrivateAccessDialogAsync(PluginStoreItem item)
    {
        var tcs = new TaskCompletionSource<string?>();

        // Guard: ensure MainWindow and its DispatcherQueue are available
        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show private access dialog: MainWindow or DispatcherQueue is null");
            return null;
        }

        var enqueued = dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                var inputBox = new TextBox
                {
                    PlaceholderText = "请输入访问密钥",
                    Width = 300
                };

                var stackPanel = new StackPanel { Spacing = 12 };
                stackPanel.Children.Add(new TextBlock
                {
                    Text = $"插件 \"{item.Name}\"ID{item.Id}为私密插件",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                stackPanel.Children.Add(inputBox);

                // Guard: XamlRoot requires a valid Content
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot show private access dialog: XamlRoot is null");
                    tcs.TrySetResult(null);
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "私密插件访问",
                    Content = stackPanel,
                    PrimaryButtonText = "确认",
                    SecondaryButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    tcs.TrySetResult(inputBox.Text.Trim());
                }
                else
                {
                    tcs.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error in private access dialog: {ex}");
                tcs.TrySetResult(null);
            }
        });

        if (!enqueued)
        {
            Debug.WriteLine("[PluginStoreVM] Failed to enqueue private access dialog to DispatcherQueue");
            return null;
        }

        return await tcs.Task;
    }
    
    private static async Task ShowMinVersionWarningAsync(PluginStoreItem item)
    {
        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show version warning: MainWindow or DispatcherQueue is null");
            return;
        }

        dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot show version warning: XamlRoot is null");
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "版本过低",
                    Content = $"插件 \"{item.Name}\" 要求启动器版本≥ {item.MinAppVersion}，当前版本为 {CurrentAppVersion}\n\n请先更新启动器后再安装此插件",
                    CloseButtonText = "知道了",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = xamlRoot
                };
                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error showing version warning: {ex}");
            }
        });
    }

    #endregion
}
