/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.ViewModels;

public partial class PluginStoreViewModel
{
    #region Private Plugin

    private async Task AddPrivatePluginAsync()
    {
        string? pluginId = null;
        string? accessKey = null;

        if (App.MainWindow?.DispatcherQueue is not { } dispatcherQueue)
        {
            Debug.WriteLine("[PluginStoreVM] Cannot add private plugin: MainWindow or DispatcherQueue is null");
            return;
        }

        var tcs = new TaskCompletionSource();
        dispatcherQueue.TryEnqueue(async () =>
        {
            try
            {
                if (App.MainWindow?.Content?.XamlRoot is not { } xamlRoot)
                {
                    Debug.WriteLine("[PluginStoreVM] Cannot add private plugin: XamlRoot is null");
                    tcs.TrySetResult();
                    return;
                }

                var idBox = new TextBox { PlaceholderText = "插件ID", Width = 300 };
                var keyBox = new TextBox { PlaceholderText = "访问密钥", Width = 300 };

                var panel = new StackPanel { Spacing = 12 };
                panel.Children.Add(new TextBlock { Text = "输入私密插件的 ID 和访问密钥：" });
                panel.Children.Add(new TextBlock { Text = "插件ID", FontSize = 12, Opacity = 0.7 });
                panel.Children.Add(idBox);
                panel.Children.Add(new TextBlock { Text = "访问密钥", FontSize = 12, Opacity = 0.7 });
                panel.Children.Add(keyBox);

                var dialog = new ContentDialog
                {
                    Title = "添加私密插件",
                    Content = panel,
                    PrimaryButtonText = "添加",
                    SecondaryButtonText = "取消",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = xamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    pluginId = idBox.Text.Trim();
                    accessKey = keyBox.Text.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Error in private plugin dialog: {ex}");
            }
            finally
            {
                tcs.TrySetResult();
            }
        });

        await tcs.Task;

        if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(accessKey))
            return;

        try
        {
            IsLoading = true;
            StatusMessage = "正在验证私密插件访问...";

            var accessResult = await _storeService.GetPrivateAccessAsync(pluginId, accessKey);
            if (accessResult.Plugin != null)
            {
                accessResult.Plugin.AccessToken = accessResult.AccessToken;
                UpdateLocalState(accessResult.Plugin);
                
                Plugins.Insert(0, accessResult.Plugin);
                TotalPlugins++;
                // 与 IsEmpty 一起维护，否则下次刷新会在已有内容上盖一层骨架屏。
                _hasContent = true;
                IsEmpty = false;
                StatusMessage = string.Format("已添加私密插件: {0}", accessResult.Plugin.Name);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] AddPrivatePlugin error: {ex.Message}");
            StatusMessage = string.Format("私密插件添加失败: {0}", ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion
}
