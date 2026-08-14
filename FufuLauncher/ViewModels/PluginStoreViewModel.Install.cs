/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class PluginStoreViewModel
{
    #region Plugin Install

    private void CancelInstall(PluginStoreItem item)
    {
        if (item == null || !item.IsInstallInProgress) return;

        Debug.WriteLine($"[PluginStoreVM] User cancelled install for plugin: {item.Id}");
        
        _installCts?.Cancel();

        item.InstallStatusText = "PluginStoreCancelling".GetLocalized();
        item.DownloadSpeedBytesPerSecond = 0;
    }

    private async Task InstallPluginAsync(PluginStoreItem item)
    {
        if (item == null || item.IsInstallInProgress) return;

        try
        {
            _installCts?.Cancel();
            _installCts = new CancellationTokenSource();

            _installingPluginIds.Add(item.Id);

            item.State = StorePluginState.Installing;
            item.IsInstallInProgress = true;
            item.InstallProgress = 0;
            item.InstallStatusText = "PluginStoreVerifying".GetLocalized();
            
            if (!string.IsNullOrWhiteSpace(item.MinAppVersion))
            {
                if (!IsVersionSatisfied(CurrentAppVersion, item.MinAppVersion))
                {
                    _installingPluginIds.Remove(item.Id);
                    await ShowMinVersionWarningAsync(item);
                    item.State = StorePluginState.Available;
                    item.InstallProgress = 0;
                    item.InstallStatusText = "PluginStoreVersionTooLow".GetLocalized();
                    return;
                }
            }
            
            if (item.IsPrivate && string.IsNullOrWhiteSpace(item.AccessToken))
            {
                var accessKey = await ShowPrivateAccessDialogAsync(item);
                if (string.IsNullOrWhiteSpace(accessKey))
                {
                    _installingPluginIds.Remove(item.Id);
                    item.State = StorePluginState.Available;
                    item.InstallProgress = 0;
                    item.InstallStatusText = string.Empty;
                    return;
                }

                try
                {
                    var accessResult = await _storeService.GetPrivateAccessAsync(item.Id, accessKey);
                    item.AccessToken = accessResult.AccessToken;
                    
                    if (accessResult.Plugin != null)
                    {
                        item.Version = accessResult.Plugin.Version;
                        item.FileHash = accessResult.Plugin.FileHash;
                        item.LuaHash = accessResult.Plugin.LuaHash;
                        item.LuaInstallUrl = accessResult.Plugin.LuaInstallUrl;
                        item.LuaUninstallUrl = accessResult.Plugin.LuaUninstallUrl;
                        item.DownloadUrl = accessResult.Plugin.DownloadUrl;
                        item.SizeBytes = accessResult.Plugin.SizeBytes;
                        item.DllFileName = accessResult.Plugin.DllFileName;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[PluginStoreVM] Private access failed: {ex.Message}");
                    _installingPluginIds.Remove(item.Id);
                    item.State = StorePluginState.Available;
                    item.InstallProgress = 0;
                    item.InstallStatusText = "PluginStorePrivateAccessDenied".GetLocalized();
                    StatusMessage = ex.Message;
                    return;
                }
            }

            await DoInstallAsync(item);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PluginStoreVM] Install error: {ex}");
            _installingPluginIds.Remove(item.Id);
            item.State = StorePluginState.Available;
            item.InstallProgress = 0;
            item.InstallStatusText = "PluginStoreInstallFailedShort".GetLocalized();
            StatusMessage = string.Format("PluginStoreInstallFailed".GetLocalized(), ex.Message);
            
            CleanupPluginDir(item.Id);
        }
        finally
        {
            _installingPluginIds.Remove(item.Id);
            item.IsInstallInProgress = false;
            _installCts?.Dispose();
            _installCts = null;
        }
    }
    
    private async Task DoInstallAsync(PluginStoreItem item)
    {
        var maxCaptchaRetries = 3;
        var attempt = 0;

        while (attempt < maxCaptchaRetries)
        {
            try
            {
                item.InstallStatusText = attempt > 0
                    ? "PluginStoreRetrying".GetLocalized()
                    : "PluginStoreDownloadingLua".GetLocalized();

                await _luaInstaller.ExecuteInstallScriptAsync(
                    item.LuaInstallUrl,
                    item.LuaHash,
                    item.FileHash,
                    _installCts?.Token ?? CancellationToken.None,
                    item.DllFileName,
                    item.Id,
                    item.DlToken,
                    item.AccessToken);

                var pluginDir = Path.Combine(_pluginsDir, item.Id);
                _luaInstaller.EnsureConfigFileEntry(pluginDir, item.DllFileName);
                
                if (!IsPluginInstalledOnDisk(item.Id, out _))
                {
                    Debug.WriteLine($"[PluginStoreVM] Install verification failed: plugin '{item.Id}' not found on disk after install script");
                    item.State = StorePluginState.Available;
                    item.InstallProgress = 0;
                    item.InstallStatusText = "PluginStoreInstallFailedShort".GetLocalized();
                    StatusMessage = string.Format("PluginStoreInstallFailed".GetLocalized(), "PluginStoreInstallVerifyFailed".GetLocalized());
                    CleanupPluginDir(item.Id);
                    return;
                }

                if (_dispatcher != null)
                {
                    var capturedItem = item;
                    _dispatcher.TryEnqueue(async () =>
                    {
                        capturedItem.InstallProgress = 100;
                        capturedItem.InstallProgressPercent = 100.0;
                        capturedItem.InstallStatusText = "PluginStoreInstallComplete".GetLocalized();
                        capturedItem.DownloadSpeedBytesPerSecond = 0;
                        
                        await Task.Delay(600);
                        
                        capturedItem.State = StorePluginState.Installed;
                    });
                }
                else
                {
                    item.InstallProgress = 100;
                    item.InstallProgressPercent = 100.0;
                    item.InstallStatusText = "PluginStoreInstallComplete".GetLocalized();
                    item.State = StorePluginState.Installed;
                }
                StatusMessage = string.Format("PluginStoreInstallSuccess".GetLocalized(), item.Name);
                return;
            }
            catch (CaptchaRequiredException captchaEx)
            {
                Debug.WriteLine($"[PluginStoreVM] Captcha required: {captchaEx.VerifyUrl}");
                item.InstallStatusText = "PluginStoreCaptchaRequired".GetLocalized();
                
                var dlToken = await ShowGeetestCaptchaAsync(captchaEx.VerifyUrl);

                if (string.IsNullOrWhiteSpace(dlToken))
                {
                    throw new OperationCanceledException("PluginStoreCaptchaCancelled".GetLocalized());
                }

                item.DlToken = dlToken;
                attempt++;
                Debug.WriteLine($"[PluginStoreVM] Got dl_token, retrying download (attempt {attempt})...");
            }
            catch (PrivatePluginAccessException privEx)
            {
                Debug.WriteLine($"[PluginStoreVM] Private access required: {privEx.Message}");
                item.InstallStatusText = "PluginStorePrivateAccessRequired".GetLocalized();

                var accessKey = await ShowPrivateAccessDialogAsync(item);
                if (string.IsNullOrWhiteSpace(accessKey))
                    throw new OperationCanceledException("PluginStorePrivateAccessCancelled".GetLocalized());

                var accessResult = await _storeService.GetPrivateAccessAsync(item.Id, accessKey);
                item.AccessToken = accessResult.AccessToken;
                if (accessResult.Plugin != null)
                {
                    item.FileHash = accessResult.Plugin.FileHash;
                    item.LuaHash = accessResult.Plugin.LuaHash;
                }
                attempt++;
            }
            catch (HashMismatchException ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Hash mismatch: {ex.Message}");
                item.State = StorePluginState.Available;
                item.InstallProgress = 0;
                item.InstallStatusText = "PluginStoreHashFailed".GetLocalized();
                StatusMessage = string.Format("PluginStoreInstallFailed".GetLocalized(), ex.Message);
                CleanupPluginDir(item.Id);
                return;
            }
            catch (SecurityViolationException ex)
            {
                Debug.WriteLine($"[PluginStoreVM] Security violation: {ex.Message}");
                item.State = StorePluginState.Available;
                item.InstallProgress = 0;
                item.InstallStatusText = "PluginStoreSecurityBlockedShort".GetLocalized();
                StatusMessage = string.Format("PluginStoreSecurityBlocked".GetLocalized(), ex.Message);
                CleanupPluginDir(item.Id);
                return;
            }
            catch (OperationCanceledException)
            {
                item.State = StorePluginState.Available;
                item.InstallProgress = 0;
                item.InstallStatusText = "PluginStoreCancelled".GetLocalized();
                CleanupPluginDir(item.Id);
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("download") || ex.Message.Contains("Download"))
            {
                Debug.WriteLine($"[PluginStoreVM] Download error (may need captcha): {ex.Message}");
                attempt++;
                if (attempt >= maxCaptchaRetries) throw;
            }
        }

        throw new InvalidOperationException("PluginStoreCaptchaRetryExhausted".GetLocalized());
    }

    private static bool IsVersionSatisfied(string currentVersion, string minVersion)
    {
        if (!AppVersionHelper.TryParseVersion(currentVersion, out var cur) ||
            !AppVersionHelper.TryParseVersion(minVersion, out var min))
        {
            return true;
        }

        return cur >= min;
    }

    private void OnInstallProgress(DownloadProgressInfo info)
    {
        _dispatcher?.TryEnqueue(() =>
        {
            foreach (var id in _installingPluginIds)
            {
                var installing = Plugins.FirstOrDefault(p => p.Id == id);
                if (installing != null && installing.State == StorePluginState.Installing)
                {
                    installing.InstallProgress = (int)Math.Round(info.Percent);
                    installing.InstallProgressPercent = info.Percent;
                    installing.InstallStatusText = info.StatusText;
                    installing.DownloadedBytes = info.BytesDownloaded;
                    installing.TotalDownloadBytes = info.TotalBytes;
                    installing.DownloadSpeedBytesPerSecond = info.SpeedBytesPerSecond;
                }
            }
        });
    }

    #endregion
}
