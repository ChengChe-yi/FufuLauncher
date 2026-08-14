/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.ViewModels;

public partial class PluginStoreViewModel
{
    #region Lua Test

    public async Task ExecuteLuaTestAsync()
    {
        string? luaCode = null;
        
        var dialogCompleted = new TaskCompletionSource<string?>();

        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot execute Lua test: MainWindow or DispatcherQueue is null");
            return;
        }

        dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot execute Lua test: XamlRoot is null");
                    dialogCompleted.TrySetResult(null);
                    return;
                }

                var inputBox = new TextBox
                {
                    PlaceholderText = "PluginStoreLuaTestInputHint".GetLocalized(),
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.NoWrap,
                    Height = 300,
                    Width = 560,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas, Cascadia Code, monospace"),
                    FontSize = 13,
                    IsSpellCheckEnabled = false
                };
                ScrollViewer.SetHorizontalScrollBarVisibility(inputBox, ScrollBarVisibility.Auto);
                ScrollViewer.SetVerticalScrollBarVisibility(inputBox, ScrollBarVisibility.Auto);

                var infoText = new TextBlock
                {
                    Text = "PluginStoreLuaTestDescription".GetLocalized(),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Opacity = 0.8,
                    Margin = new Thickness(0, 0, 0, 12)
                };

                var panel = new StackPanel { Spacing = 8 };
                panel.Children.Add(infoText);
                panel.Children.Add(inputBox);

                var dialog = new ContentDialog
                {
                    Title = "PluginStoreLuaTestTitle".GetLocalized(),
                    Content = panel,
                    PrimaryButtonText = "PluginStoreLuaTestRun".GetLocalized(),
                    SecondaryButtonText = "PluginStoreLuaTestClose".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputBox.Text))
                {
                    dialogCompleted.TrySetResult(inputBox.Text.Trim());
                }
                else
                {
                    dialogCompleted.TrySetResult(null);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Lua test dialog error: {ex.Message}");
                dialogCompleted.TrySetResult(null);
            }
        });

        luaCode = await dialogCompleted.Task;
        if (string.IsNullOrWhiteSpace(luaCode))
            return;
        
        var securityResult = PluginVerifier.ValidateLuaSecurity(luaCode);
        if (!securityResult.IsValid)
        {
            var proceed = await ShowLuaTestSecurityWarningAsync(securityResult.Reason ?? "Unknown security issue");
            if (!proceed)
            {
                StatusMessage = string.Format("Lua 测试已取消（安全阻止: {0}）", securityResult.Reason);
                return;
            }
        }
        
        StatusMessage = "PluginStoreLuaTestExecuting".GetLocalized();
        bool success = false;
        string? errorMessage = null;

        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            await _luaInstaller.ExecuteUserScriptAsync(luaCode, cts.Token);
            success = true;
        }
        catch (SecurityViolationException ex)
        {
            errorMessage = string.Format("安全违规: {0}", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            errorMessage = string.Format("Lua 执行错误: {0}", ex.Message);
        }
        catch (OperationCanceledException)
        {
            errorMessage = "脚本执行超时（5分钟）";
        }
        catch (Exception ex)
        {
            errorMessage = string.Format("未预期的错误: {0}", ex.Message);
        }
        
        var logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        var logFileName = $"lua_test_{DateTime.Now:yyyyMMdd_HHmmss}.log";
        var logFilePath = Path.Combine(logDir, logFileName);

        try
        {
            _luaInstaller.SaveLogsToFile(logFilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Failed to save log file: {ex.Message}");
            errorMessage = (errorMessage != null)
                ? errorMessage + $"\n日志保存失败: {ex.Message}"
                : $"日志保存失败: {ex.Message}";
        }
        
        await ShowLuaTestResultDialogAsync(success, logFilePath, errorMessage);

        StatusMessage = success
            ? "Lua 脚本测试完成"
            : string.Format("Lua 脚本测试失败: {0}", errorMessage ?? "未知错误");
    }

    private static async Task<bool> ShowLuaTestSecurityWarningAsync(string reason)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show security warning: MainWindow or DispatcherQueue is null");
            return false;
        }

        var enqueued = dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot show security warning: XamlRoot is null");
                    tcs.TrySetResult(false);
                    return;
                }

                var dialog = new ContentDialog
                {
                    Title = "PluginStoreLuaTestSecurityWarning".GetLocalized(),
                    Content = string.Format("PluginStoreLuaTestSecurityBlocked".GetLocalized(), reason),
                    PrimaryButtonText = "强制执行（不推荐）",
                    SecondaryButtonText = "取消",
                    DefaultButton = ContentDialogButton.Secondary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                tcs.TrySetResult(result == ContentDialogResult.Primary);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error showing security warning: {ex}");
                tcs.TrySetResult(false);
            }
        });

        if (!enqueued)
        {
            Debug.WriteLine("[PluginStoreVM] Failed to enqueue security warning to DispatcherQueue");
            return false;
        }

        return await tcs.Task;
    }

    private static async Task ShowLuaTestResultDialogAsync(bool success, string logPath, string? errorMessage)
    {
        var tcs = new TaskCompletionSource<bool>();

        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot show Lua test result: MainWindow or DispatcherQueue is null");
            return;
        }

        var enqueued = dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot show Lua test result: XamlRoot is null");
                    tcs.TrySetResult(true);
                    return;
                }

                var messagePanel = new StackPanel { Spacing = 12 };

                var statusIcon = success ? "\uE73E" : "\uE783"; // Checkmark or Error
                var statusColor = success
                    ? new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.LimeGreen)
                    : new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.OrangeRed);

                var statusRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                statusRow.Children.Add(new FontIcon
                {
                    Glyph = statusIcon,
                    FontSize = 20,
                    Foreground = statusColor
                });
                statusRow.Children.Add(new TextBlock
                {
                    Text = success ? "PluginStoreLuaTestSuccess".GetLocalized() : "PluginStoreLuaTestFailed".GetLocalized(),
                    FontSize = 16,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                });
                messagePanel.Children.Add(statusRow);

                if (!string.IsNullOrEmpty(errorMessage))
                {
                    messagePanel.Children.Add(new TextBlock
                    {
                        Text = errorMessage,
                        Foreground = statusColor,
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 13
                    });
                }

                messagePanel.Children.Add(new TextBlock
                {
                    Text = string.Format("PluginStoreLuaTestLogSaved".GetLocalized(), logPath),
                    TextWrapping = TextWrapping.Wrap,
                    FontSize = 13,
                    Opacity = 0.8,
                    Margin = new Thickness(0, 8, 0, 0)
                });

                var dialog = new ContentDialog
                {
                    Title = success ? "PluginStoreLuaTestSuccess".GetLocalized() : "PluginStoreLuaTestFailed".GetLocalized(),
                    Content = messagePanel,
                    PrimaryButtonText = "PluginStoreLuaTestOpenLog".GetLocalized(),
                    SecondaryButtonText = "PluginStoreLuaTestClose".GetLocalized(),
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    try
                    {
                        // Open the log file with the system default text editor
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = logPath,
                            UseShellExecute = true
                        };
                        System.Diagnostics.Process.Start(psi);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[PluginStoreVM] Failed to open log file: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error showing Lua test result: {ex}");
            }
            finally
            {
                tcs.TrySetResult(true);
            }
        });

        if (!enqueued)
        {
            Debug.WriteLine("[PluginStoreVM] Failed to enqueue Lua test result to DispatcherQueue");
            return;
        }

        await tcs.Task;
    }

    #endregion
}
