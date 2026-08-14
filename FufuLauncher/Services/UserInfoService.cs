/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models.MiHoYo;
using Microsoft.Extensions.Logging;

namespace FufuLauncher.Services;

public class UserInfoService : IUserInfoService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UserInfoService> _logger;
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IHoyolabRoleResolverService _hoyolabRoleResolverService;

    public UserInfoService(
        ILogger<UserInfoService> logger,
        ILocalSettingsService localSettingsService,
        IHoyolabRoleResolverService hoyolabRoleResolverService)
    {
        _logger = logger;
        _localSettingsService = localSettingsService;
        _hoyolabRoleResolverService = hoyolabRoleResolverService;
        _httpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        });
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    private void ApplyCommonHeaders(string cookie)
    {
        
        var keys = new[] { "ltoken", "ltuid", "cookie_token", "account_id", "ltoken_v2", "ltuid_v2", "cookie_token_v2", "account_id_v2" };
        var found = keys.Where(k => cookie.Contains(k + "=", StringComparison.OrdinalIgnoreCase)).ToArray();
        var missing = keys.Where(k => !found.Contains(k, StringComparer.OrdinalIgnoreCase)).ToArray();
        System.Diagnostics.Debug.WriteLine($"[UserInfoService] Cookie length={cookie.Length}, found=[{string.Join(", ", found)}], missing=[{string.Join(", ", missing)}]");

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("DS", GenerateDS());
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-rpc-device_id", Guid.NewGuid().ToString("N"));
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-rpc-client_type", "5");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://act.mihoyo.com/");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://act.mihoyo.com");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Linux; Android 12; Unspecified Device) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/103.0.5060.129 Mobile Safari/537.36 miHoYoBBS/2.93.1");
    }
    
    private void ApplyOverseaHeaders(string cookie)
    {
        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-rpc-app_version", HeaderVersions.BbsOs254);
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-rpc-client_type", "5");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-rpc-language", "zh-cn");
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("x-rpc-device_id", Guid.NewGuid().ToString("N"));
        _httpClient.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgents.WindowsBbsOversea254);
    }

    private string GenerateDS()
    {
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var r = new Random().Next(100000, 200000).ToString();
        var c = System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes($"salt=xV8v4Qu54lUKrEYFZkJhB8cuoh9NXmz9&t={t}&r={r}")
        );
        return $"{t},{r},{BitConverter.ToString(c).Replace("-", "").ToLower()}";
    }

    private async Task<bool> IsInternationalAsync(string cookie)
    {
        var hasCnFields = cookie.Contains("ltuid=", StringComparison.OrdinalIgnoreCase) ||
                          cookie.Contains("stuid=", StringComparison.OrdinalIgnoreCase);
        var hasOsFields = cookie.Contains("ltuid_v2=", StringComparison.OrdinalIgnoreCase) ||
                          cookie.Contains("account_id_v2=", StringComparison.OrdinalIgnoreCase) ||
                          cookie.Contains("cookie_token_v2=", StringComparison.OrdinalIgnoreCase);
        
        if (hasCnFields) return false;
        if (hasOsFields) return true;

        var isOsObj = await _localSettingsService.ReadSettingAsync("IsInternationalAccount");
        return isOsObj is bool isOs && isOs;
    }

    public async Task<GameRolesResponse> GetUserGameRolesAsync(string cookie)
    {
        try
        {
            bool isOs = await IsInternationalAsync(cookie);

            if (isOs)
            {
                ApplyOverseaHeaders(cookie);
                var bindingRoles = await TryGetOverseaRolesFromBindingAsync();
                if (bindingRoles != null)
                    return new GameRolesResponse(0, "OK", new GameRolesData(bindingRoles));

                var rolesResult = await _hoyolabRoleResolverService.ResolveRolesAsync(cookie);
                return new GameRolesResponse(rolesResult.RetCode, rolesResult.Message, new GameRolesData(rolesResult.Roles));
            }

            ApplyCommonHeaders(cookie);
            var response = await _httpClient.GetAsync(ApiEndpoints.MihoyoBbsUserGameRolesUrl);
            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[UserInfoService] GameRoles HTTP {response.StatusCode} | Body({json?.Length ?? 0}): {(json?.Length > 300 ? json[..300] : json ?? "(null)")}");
            return JsonSerializer.Deserialize<GameRolesResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取角色信息失败");
            return new GameRolesResponse(-1, ex.Message, null);
        }
    }
    
    private async Task<List<GameRoleInfo>?> TryGetOverseaRolesFromBindingAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync(ApiEndpoints.OverseaUserGameRolesUrl);
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GameRolesResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (result == null || result.retcode != 0 || result.data?.list == null || result.data.list.Count == 0)
                return null;
            
            return result.data.list
                .Select(role => string.IsNullOrEmpty(role.region)
                    ? role with { region = ServerRegion.Resolve(role.game_uid) }
                    : role)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "国际服 binding 角色解析失败，回退 RoleResolver");
            return null;
        }
    }


    public async Task<UserFullInfoResponse> GetUserFullInfoAsync(string cookie)
    {
        try
        {
            bool isOs = await IsInternationalAsync(cookie);
            if (isOs)
                ApplyOverseaHeaders(cookie);
            else
                ApplyCommonHeaders(cookie);

            var url = isOs ? ApiEndpoints.OverseaUserFullInfoUrl : ApiEndpoints.MiyousheUserFullInfoUrl;

            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();
            System.Diagnostics.Debug.WriteLine($"[UserInfoService] UserFullInfo HTTP {response.StatusCode} | URL: {url} | Body({json?.Length ?? 0}): {(json?.Length > 300 ? json[..300] : json ?? "(null)")}");
            return JsonSerializer.Deserialize<UserFullInfoResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取用户信息失败");
            return new UserFullInfoResponse(-1, ex.Message, null);
        }
    }

    public async Task<GameRecordCardResponse> GetGameRecordCardAsync(string stuid, string cookie)
    {
        return await Task.FromResult(new GameRecordCardResponse(-1, "UserInfo_FeatureRemoved".GetLocalized(), null));
    }
}

