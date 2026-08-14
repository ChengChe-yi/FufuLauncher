/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Services;
using Windows.Storage.Pickers;

namespace FufuLauncher.ViewModels;

public partial class OtherViewModel
{
    #region Additional Program

    partial void OnAdditionalProgramPathChanged(string value)
    {
        IsApplyButtonEnabled = !string.IsNullOrWhiteSpace(value);

        if (!string.IsNullOrWhiteSpace(value))
        {
            var trimmedPath = value.Trim('"');
            if (File.Exists(trimmedPath) && Path.GetExtension(trimmedPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "路径有效";
            }
            else
            {
                StatusMessage = "文件不存在或不是有效的 .exe 文件";
            }
        }
        else
        {
            StatusMessage = string.Empty;
        }
    }

    private async Task ApplyProgramPathAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(AdditionalProgramPath))
            {
                StatusMessage = "路径不能为空";
                return;
            }

            var trimmedPath = AdditionalProgramPath.Trim('"');

            if (File.Exists(trimmedPath) && System.IO.Path.GetExtension(trimmedPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "路径已应用";

                await SaveSettingsAsync();

                _ = Task.Delay(2000).ContinueWith(_ =>
                    _dispatcherQueue?.TryEnqueue(() => StatusMessage = string.Empty));
            }
            else
            {
                StatusMessage = "无效的路径，请检查文件是否存在且为 .exe 格式";

                var savedPath = await _localSettingsService.ReadSettingAsync("AdditionalProgramPath");
                if (savedPath != null)
                {
                    AdditionalProgramPath = savedPath.ToString().Trim('"');
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"应用失败: {ex.Message}";
            Debug.WriteLine($"[ApplyProgramPathAsync] 失败: {ex.Message}");
        }
    }

    private async Task BrowseProgramAsync()
    {
        try
        {
            if (!_dispatcherQueue.HasThreadAccess)
            {
                Debug.WriteLine("[错误] BrowseProgramAsync 不在UI线程上执行");
                return;
            }

            var path = await FilePickerService.PickOpenFileAsync(
                null,
                new[] { ("可执行文件", new[] { ".exe" }) },
                PickerLocationId.Desktop,
                async msg => { StatusMessage = msg; await ShowErrorAsync(msg); });

            if (!string.IsNullOrEmpty(path))
            {
                path = path.Trim('"');
                Debug.WriteLine($"[OtherViewModel] 用户选择程序: '{path}'");

                if (File.Exists(path))
                {
                    AdditionalProgramPath = path;
                }
                else
                {
                    await ShowErrorAsync("文件不存在或无法访问");
                }
            }
            else
            {
                Debug.WriteLine("[OtherViewModel] 用户取消了文件选择");
            }
        }
        catch (UnauthorizedAccessException)
        {
            await ShowErrorAsync("权限错误：请以普通用户身份运行程序选择文件");
            Debug.WriteLine("[严重错误] 管理员模式权限问题");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"选择程序失败: {ex.Message}\n堆栈: {ex.StackTrace}");
            await ShowErrorAsync($"选择程序失败: {ex.Message}");
        }
    }

    #endregion
}
