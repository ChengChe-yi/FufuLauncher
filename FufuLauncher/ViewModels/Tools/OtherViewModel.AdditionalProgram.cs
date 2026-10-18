/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Helpers;
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
            if (File.Exists(trimmedPath) &&
                Path.GetExtension(trimmedPath).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "AdditionalProgram_PathValid".GetLocalized();
            }
            else
            {
                StatusMessage = "AdditionalProgram_ExeNotFoundOrInvalid".GetLocalized();
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
                StatusMessage = "AdditionalProgram_PathEmpty".GetLocalized();
                return;
            }

            var trimmedPath = AdditionalProgramPath.Trim('"');

            if (File.Exists(trimmedPath) && System.IO.Path.GetExtension(trimmedPath)
                    .Equals(".exe", StringComparison.OrdinalIgnoreCase))
            {
                StatusMessage = "AdditionalProgram_PathApplied".GetLocalized();

                await SaveSettingsAsync();

                _ = Task.Delay(2000).ContinueWith(_ =>
                    _dispatcherQueue?.TryEnqueue(() => StatusMessage = string.Empty));
            }
            else
            {
                StatusMessage = "AdditionalProgram_InvalidPathCheckExe".GetLocalized();

                var savedPath = await _localSettingsService.ReadSettingAsync("AdditionalProgramPath");
                if (savedPath != null)
                {
                    AdditionalProgramPath = savedPath.ToString().Trim('"');
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessage = string.Format("AdditionalProgram_ApplyFailed_Format".GetLocalized(), ex.Message);
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
                new[] { ("Common_ExecutableFiles".GetLocalized(), new[] { ".exe" }) },
                PickerLocationId.Desktop,
                async msg =>
                {
                    StatusMessage = msg;
                    await ShowErrorAsync(msg);
                });

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
                    await ShowErrorAsync("AdditionalProgram_FileNotFoundOrInaccessible".GetLocalized());
                }
            }
            else
            {
                Debug.WriteLine("[OtherViewModel] 用户取消了文件选择");
            }
        }
        catch (UnauthorizedAccessException)
        {
            await ShowErrorAsync("AdditionalProgram_PermissionOrOccupied".GetLocalized());
            Debug.WriteLine("[严重错误] 管理员模式权限问题");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"选择程序失败: {ex.Message}\n堆栈: {ex.StackTrace}");
            await ShowErrorAsync(string.Format("AdditionalProgram_SelectFailed_Format".GetLocalized(), ex.Message));
        }
    }

    #endregion
}