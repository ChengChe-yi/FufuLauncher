/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using Microsoft.UI.Xaml.Controls;
using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region UI Interaction

    private void RegisterUiHandlers(Table table)
    {
        table["show_notification"] = (Action<string, string, string, int>)((title, message, typeStr, duration) =>
        {
            LogMessage($"通知: [{typeStr}] {title} - {message}");

            var type = typeStr?.ToLowerInvariant() switch
            {
                "success" => NotificationType.Success,
                "warning" => NotificationType.Warning,
                "error" => NotificationType.Error,
                _ => NotificationType.Information
            };

            if (duration <= 0) duration = 5000;

            WeakReferenceMessenger.Default.Send(new NotificationMessage(title, message, type, duration));
        });

        table["show_dialog"] =
            (Func<string, string, string, string, string, string>)((title, content, primaryText, secondaryText,
                closeText) =>
            {
                LogMessage($"弹窗: {title}");

                var dispatcher = UIDispatcher;
                var xamlRoot = MainXamlRoot;

                if (dispatcher == null)
                {
                    LogMessage("弹窗失败: UI 调度器未初始化");
                    return "none";
                }

                var tcs = new TaskCompletionSource<string>();

                dispatcher.TryEnqueue(async () =>
                {
                    try
                    {
                        var dialog = new ContentDialog
                        {
                            Title = title,
                            Content = content,
                            XamlRoot = xamlRoot,
                            DefaultButton = ContentDialogButton.Primary
                        };

                        if (!string.IsNullOrEmpty(primaryText))
                            dialog.PrimaryButtonText = primaryText;
                        if (!string.IsNullOrEmpty(secondaryText))
                            dialog.SecondaryButtonText = secondaryText;
                        if (!string.IsNullOrEmpty(closeText))
                            dialog.CloseButtonText = closeText;
                        else
                            dialog.CloseButtonText = "PluginStoreDialogClose".GetLocalized();

                        var result = await dialog.ShowAsync();
                        tcs.TrySetResult(result.ToString().ToLowerInvariant());
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[LuaInstaller] Dialog error: {ex.Message}");
                        tcs.TrySetResult("error");
                    }
                });

                try
                {
                    return tcs.Task.GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LuaInstaller] Dialog wait error: {ex.Message}");
                    return "error";
                }
            });
    }

    #endregion

    private static async Task<string?> RequestArchivePasswordAsync(string archiveName, bool retry,
        CancellationToken cancellationToken)
    {
        var dispatcher = UIDispatcher;
        if (dispatcher == null || MainXamlRoot == null)
            throw new PluginArchivePasswordException("PluginStoreArchivePasswordUnavailable".GetLocalized());

        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        ContentDialog? dialog = null;
        using var registration = cancellationToken.Register(() =>
        {
            completion.TrySetCanceled(cancellationToken);
            dispatcher.TryEnqueue(() => dialog?.Hide());
        });
        if (!dispatcher.TryEnqueue(async () =>
            {
                if (cancellationToken.IsCancellationRequested) return;
                try
                {
                    var passwordBox = new PasswordBox
                    {
                        PlaceholderText = "PluginStoreArchivePasswordPlaceholder".GetLocalized(),
                        MinWidth = 300
                    };
                    var panel = new StackPanel { Spacing = 12 };
                    panel.Children.Add(new TextBlock
                    {
                        Text = string.Format((retry ? "PluginStoreArchivePasswordRetry" :
                            "PluginStoreArchivePasswordPrompt").GetLocalized(), archiveName),
                        TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap
                    });
                    panel.Children.Add(passwordBox);
                    dialog = new ContentDialog
                    {
                        Title = "PluginStoreArchivePasswordTitle".GetLocalized(),
                        Content = panel,
                        PrimaryButtonText = "PluginStoreArchivePasswordExtract".GetLocalized(),
                        CloseButtonText = "PluginStoreArchivePasswordCancel".GetLocalized(),
                        DefaultButton = ContentDialogButton.Primary,
                        XamlRoot = MainXamlRoot
                    };
                    var result = await dialog.ShowAsync();
                    completion.TrySetResult(result == ContentDialogResult.Primary ? passwordBox.Password : null);
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            }))
            throw new PluginArchivePasswordException("PluginStoreArchivePasswordUnavailable".GetLocalized());

        return await completion.Task.ConfigureAwait(false);
    }
}
