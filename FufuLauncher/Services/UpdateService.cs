/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FufuLauncher.Constants;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services;

public class UpdateService : IUpdateService
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly HttpClient _httpClient;
    private static readonly string CurrentVersion = AppVersionHelper.NumericVersion;

    public UpdateService(ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;

        var handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            UseCookies = false
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestHeaders =
            {
                UserAgent = { new System.Net.Http.Headers.ProductInfoHeaderValue("Fufu-Launcher", CurrentVersion) },
                Accept = { new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json") }
            }
        };
    }

    public async Task<UpdateCheckResult> CheckUpdateAsync()
    {
        try
        {
            Debug.WriteLine($"[UpdateService] === 版本检查开始 ===");
            Debug.WriteLine($"[UpdateService] 本地版本: {CurrentVersion}");
            Debug.WriteLine($"[UpdateService] 超时设置: {_httpClient.Timeout.TotalSeconds} 秒");

            if (!await IsServerReachableAsync())
            {
                Debug.WriteLine("[UpdateService] 服务器暂时不可达，跳过版本检查");
                return new UpdateCheckResult { ShouldShowUpdate = false };
            }

            var json = await GetWithRetryAsync(ApiEndpoints.UpdateJsonUrl, ApiEndpoints.UpdateJsonFallbackUrl, maxRetries: 3);
            Debug.WriteLine($"[UpdateService] 服务器响应: {json}");

            var updateInfo = JsonSerializer.Deserialize<UpdateInfo>(json);
            var serverVersion = updateInfo?.Version ?? CurrentVersion;
            var updateInfoUrl = updateInfo?.UpdateInfoUrl ?? ApiEndpoints.UpdateHtmlUrl;
            var previewVersion = updateInfo?.PreReleaseVersion ?? string.Empty;
            var previewUpdateInfoUrl = string.IsNullOrEmpty(updateInfo?.PreReleaseUpdateInfoUrl)
                ? ApiEndpoints.UpdatePreHtmlUrl
                : updateInfo!.PreReleaseUpdateInfoUrl;

            Debug.WriteLine($"[UpdateService] 解析后的服务器版本: {serverVersion}");
            Debug.WriteLine($"[UpdateService] 解析后的预览版版本: '{previewVersion}'");
            Debug.WriteLine($"[UpdateService] 更新公告URL: {updateInfoUrl}");
            Debug.WriteLine($"[UpdateService] 预览版更新公告URL: {previewUpdateInfoUrl}");

            var lastVersionObj = await _localSettingsService.ReadSettingAsync(LocalSettingsService.LastAnnouncedVersionKey);
            var lastVersion = lastVersionObj?.ToString() ?? string.Empty;

            Debug.WriteLine($"[UpdateService] 上次记录版本: '{lastVersion}'");
            
            if (IsNewerVersion(serverVersion, CurrentVersion) && serverVersion != lastVersion)
            {
                Debug.WriteLine($"[UpdateService] 发现新版本，准备显示更新窗口");
                await _localSettingsService.SaveSettingAsync(LocalSettingsService.LastAnnouncedVersionKey, serverVersion);

                return new UpdateCheckResult
                {
                    ShouldShowUpdate = true,
                    ServerVersion = serverVersion,
                    UpdateInfoUrl = updateInfoUrl
                };
            }
            
            var previewAnnouncementEnabledObj = await _localSettingsService.ReadSettingAsync("IsPreviewUpdateAnnouncementEnabled");
            var previewAnnouncementEnabled = previewAnnouncementEnabledObj == null || Convert.ToBoolean(previewAnnouncementEnabledObj);

            if (previewAnnouncementEnabled &&
                IsNewerVersion(previewVersion, serverVersion) && IsNewerVersion(previewVersion, CurrentVersion))
            {
                var lastPreviewVersionObj = await _localSettingsService.ReadSettingAsync(LocalSettingsService.LastAnnouncedPreviewVersionKey);
                var lastPreviewVersion = lastPreviewVersionObj?.ToString() ?? string.Empty;

                Debug.WriteLine($"[UpdateService] 上次记录预览版版本: '{lastPreviewVersion}'");

                if (previewVersion != lastPreviewVersion)
                {
                    Debug.WriteLine($"[UpdateService] 发现新预览版，准备显示更新窗口");
                    await _localSettingsService.SaveSettingAsync(LocalSettingsService.LastAnnouncedPreviewVersionKey, previewVersion);

                    return new UpdateCheckResult
                    {
                        ShouldShowUpdate = true,
                        IsPreview = true,
                        ServerVersion = previewVersion,
                        UpdateInfoUrl = previewUpdateInfoUrl
                    };
                }

                Debug.WriteLine($"[UpdateService] 已显示过此预览版，跳过重复提示");
            }

            Debug.WriteLine($"[UpdateService] 无可用更新，跳过");
            return new UpdateCheckResult { ShouldShowUpdate = false };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateService] 检查失败: {ex.GetType().Name} - {ex.Message}");
            Debug.WriteLine($"[UpdateService] 堆栈: {ex.StackTrace}");
            return new UpdateCheckResult { ShouldShowUpdate = false };
        }
    }

    internal static bool IsNewerVersion(string serverVersion, string currentVersion)
    {
        if (!AppVersionHelper.TryParseVersion(serverVersion, out var serverVer) ||
            !AppVersionHelper.TryParseVersion(currentVersion, out var currentVer))
        {
            return false;
        }

        return serverVer > currentVer;
    }

    private async Task<bool> IsServerReachableAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync(ApiEndpoints.AgreementUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            if (response.IsSuccessStatusCode) return true;
        }
        catch { }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var response = await _httpClient.GetAsync(ApiEndpoints.AgreementFallbackUrl, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> GetWithRetryAsync(string url, int maxRetries)
    {
        return await GetWithRetryAsync(url, null, maxRetries);
    }

    private async Task<string> GetWithRetryAsync(string url, string? fallbackUrl, int maxRetries)
    {
        Exception? lastException = null;

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                Debug.WriteLine($"[UpdateService] 请求尝试 {i + 1}/{maxRetries}: {url}");
                return await _httpClient.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UpdateService] 尝试 {i + 1} 失败: {ex.Message}");
                lastException = ex;

                if (i == 0 && fallbackUrl != null)
                {
                    Debug.WriteLine($"[UpdateService] 切换到备用地址: {fallbackUrl}");
                    url = fallbackUrl;
                    fallbackUrl = null;
                    continue;
                }

                if (i < maxRetries - 1)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
                }
            }
        }

        throw lastException ?? new Exception("Update_AllRetriesFailed".GetLocalized());
    }

    private class UpdateInfo
    {
        [JsonPropertyName("Version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("updateInfoUrl")]
        public string UpdateInfoUrl { get; set; } = string.Empty;

        [JsonPropertyName("PreReleaseVersion")]
        public string PreReleaseVersion { get; set; } = string.Empty;

        [JsonPropertyName("PreReleaseUpdateInfoUrl")]
        public string PreReleaseUpdateInfoUrl { get; set; } = string.Empty;
    }
}
