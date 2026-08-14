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

        table["show_dialog"] = (Func<string, string, string, string, string, string>)((title, content, primaryText, secondaryText, closeText) =>
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
}
