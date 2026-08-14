/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Models;
using MihoyoBBS;

namespace FufuLauncher.Services;

public partial class AccountManager
{
    #region 旧账号数据迁移

    private async Task<bool> MigrateLegacyCookieFileAsync(AccountEntry account)
    {
        if (string.IsNullOrEmpty(account.CookieFilePath))
            return false;

        string path = Path.Combine(CookiesDir, account.CookieFilePath);
        if (!File.Exists(path))
            return false;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return false;

            if (TryGetPropertyIgnoreCase(root, "cookies", out var cookiesProp)
                && cookiesProp.ValueKind == JsonValueKind.Object)
                return false;

            Dictionary<string, string> cookies;
            if (TryGetPropertyIgnoreCase(root, "values", out var valuesProp)
                && valuesProp.ValueKind == JsonValueKind.Object)
            {
                cookies = ReadStringDictionary(valuesProp);
            }
            else
            {
                cookies = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                          ?? new Dictionary<string, string>();
            }

            await WriteCookieFileAsync(path, cookies);
            System.Diagnostics.Debug.WriteLine(
                $"[AccountManager] 已迁移遗留 Cookie 文件: {account.CookieFilePath}");
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AccountManager] Cookie 文件迁移失败 {account.CookieFilePath}: {ex.Message}");
            return false;
        }
    }

    private bool HasLegacyAccounts()
    {
        if (!Directory.Exists(DataDir))
            return false;

        var configFiles = Directory.GetFiles(DataDir, "config*.json")
            .Where(f => !Path.GetFileName(f).Equals("accounts.json", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return configFiles.Count > 0;
    }

    private async Task MigrateLegacyAccountsAsync()
    {
        System.Diagnostics.Debug.WriteLine("[AccountManager] 开始迁移旧账号数据...");

        try
        {
            var subAccountFiles = new List<string>();
            if (Directory.Exists(DataDir))
            {
                subAccountFiles.AddRange(
                    Directory.GetFiles(DataDir, "config*.json")
                        .Where(f =>
                        {
                            var name = Path.GetFileName(f);
                            return !name.Equals("config.json", StringComparison.OrdinalIgnoreCase) &&
                                   !name.Equals("config.lab.json", StringComparison.OrdinalIgnoreCase) &&
                                   !name.Equals("accounts.json", StringComparison.OrdinalIgnoreCase);
                        })
                );
            }

            bool hasOnlyMainConfig = subAccountFiles.Count == 0;

            var processed = new HashSet<string>();
            var migratedConfigFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int migratedCount = 0;

            foreach (var configFile in subAccountFiles)
            {
                try
                {
                    var fileName = Path.GetFileName(configFile);
                    var json = await File.ReadAllTextAsync(configFile);
                    var config = JsonSerializer.Deserialize<Config>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (config?.Account == null || string.IsNullOrWhiteSpace(config.Account.Cookie))
                        continue;

                    var cookieDict = ParseCookieString(config.Account.Cookie);
                    if (cookieDict.Count == 0)
                        continue;

                    string stuid = config.Account.Stuid;
                    if (string.IsNullOrWhiteSpace(stuid))
                    {
                        if (cookieDict.TryGetValue("ltuid", out var ltuid))
                            stuid = ltuid;
                        else if (cookieDict.TryGetValue("ltuid_v2", out var ltuidV2))
                            stuid = ltuidV2;
                    }

                    if (string.IsNullOrWhiteSpace(stuid))
                        continue;

                    if (processed.Contains(stuid))
                    {
                        migratedConfigFiles.Add(configFile);
                        continue;
                    }

                    string serverType = DetermineServerTypeByFileName(fileName);
                    string accountId = $"{serverType}_{stuid}";

                    if (_accountList.Accounts.Any(a => a.Id == accountId))
                    {
                        System.Diagnostics.Debug.WriteLine($"[AccountManager] 账号 {accountId} 已存在，确认迁移完成");
                        processed.Add(stuid);
                        migratedConfigFiles.Add(configFile);
                        continue;
                    }

                    string cookieFileName = $"{accountId}.json";
                    string cookiePath = Path.Combine(CookiesDir, cookieFileName);
                    await WriteCookieFileAsync(cookiePath, cookieDict);

                    var entry = new AccountEntry
                    {
                        Id = accountId,
                        Stuid = stuid,
                        ServerType = serverType,
                        CookieFilePath = cookieFileName,
                        Nickname = config.Display?.Nickname ?? "",
                        AvatarUrl = config.Display?.AvatarUrl ?? "",
                        GameUid = config.Display?.GameUid ?? "",
                        LastLoginTime = DateTime.Now,
                        CookieVersion = CookieFileVersion,
                        UpdatedAt = DateTime.Now
                    };

                    _accountList.Accounts.Add(entry);
                    processed.Add(stuid);
                    migratedConfigFiles.Add(configFile);
                    migratedCount++;

                    // 迁移云游戏凭证到 LocalSettings
                    var cloudToken = config.Account.CloudComboToken;
                    if (!string.IsNullOrWhiteSpace(cloudToken))
                    {
                        try
                        {
                            var settings = App.GetService<ILocalSettingsService>();
                            await settings.SaveSettingAsync($"CloudComboToken_{stuid}", cloudToken);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[AccountManager] 迁移云游戏凭证失败: {ex.Message}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[AccountManager] 已迁移账号: {accountId} ({entry.Nickname}) [{serverType}]");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AccountManager] 迁移文件 {configFile} 失败: {ex.Message}");
                }
            }

            if (migratedCount > 0)
            {
                await SaveAccountListAsync();
                System.Diagnostics.Debug.WriteLine(
                    $"[AccountManager] 迁移完成，共迁移 {migratedCount} 个账号");
            }

            if (migratedCount > 0 || migratedConfigFiles.Count > 0)
            {
                var activeConfigFile = await MigrateActiveAccountAsync();
                if (activeConfigFile != null)
                    migratedConfigFiles.Add(activeConfigFile);
            }
            else if (hasOnlyMainConfig)
            {
                // 没有子账号文件，尝试从主 config.json / config.lab.json 迁移唯一账号
                var settings = App.GetService<ILocalSettingsService>();
                bool isInternationalAccount = false;
                try
                {
                    var isOsObj = await settings.ReadSettingAsync("IsInternationalAccount");
                    isInternationalAccount = isOsObj is bool b && b;
                }
                catch { }

                string mainConfigPath = isInternationalAccount
                    ? Path.Combine(DataDir, "config.lab.json")
                    : Path.Combine(DataDir, "config.json");

                if (File.Exists(mainConfigPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(mainConfigPath);
                        var config = JsonSerializer.Deserialize<Config>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (config?.Account != null && !string.IsNullOrWhiteSpace(config.Account.Cookie))
                        {
                            var cookieDict = ParseCookieString(config.Account.Cookie);
                            string stuid = config.Account.Stuid;
                            if (string.IsNullOrWhiteSpace(stuid))
                            {
                                if (cookieDict.TryGetValue("ltuid", out var ltuid))
                                    stuid = ltuid;
                                else if (cookieDict.TryGetValue("ltuid_v2", out var ltuidV2))
                                    stuid = ltuidV2;
                            }

                            if (!string.IsNullOrWhiteSpace(stuid) && cookieDict.Count > 0)
                            {
                                string serverType = isInternationalAccount ? "os" : "cn";
                                string accountId = $"{serverType}_{stuid}";

                                string cookieFileName = $"{accountId}.json";
                                string cookiePath = Path.Combine(CookiesDir, cookieFileName);
                                await WriteCookieFileAsync(cookiePath, cookieDict);

                                var entry = new AccountEntry
                                {
                                    Id = accountId,
                                    Stuid = stuid,
                                    ServerType = serverType,
                                    CookieFilePath = cookieFileName,
                                    Nickname = config.Display?.Nickname ?? "",
                                    AvatarUrl = config.Display?.AvatarUrl ?? "",
                                    GameUid = config.Display?.GameUid ?? "",
                                    LastLoginTime = DateTime.Now,
                                    CookieVersion = CookieFileVersion,
                                    UpdatedAt = DateTime.Now
                                };

                                _accountList.Accounts.Add(entry);
                                await SaveAccountListAsync();
                                await SetActiveAccountIdAsync(accountId);
                                migratedConfigFiles.Add(mainConfigPath);
                                migratedCount = 1;

                                // 迁移云游戏凭证
                                var cloudToken = config.Account.CloudComboToken;
                                if (!string.IsNullOrWhiteSpace(cloudToken))
                                {
                                    try
                                    {
                                        await settings.SaveSettingAsync($"CloudComboToken_{stuid}", cloudToken);
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine(
                                            $"[AccountManager] 迁移云游戏凭证失败: {ex.Message}");
                                    }
                                }

                                System.Diagnostics.Debug.WriteLine(
                                    $"[AccountManager] 已从主配置迁移唯一账号: {accountId} ({entry.Nickname}) [{serverType}]");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[AccountManager] 从主配置迁移账号失败: {ex.Message}");
                    }
                }

                if (migratedCount == 0)
                    System.Diagnostics.Debug.WriteLine("[AccountManager] 未找到需要迁移的账号");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[AccountManager] 未找到需要迁移的账号");
            }

            foreach (var file in migratedConfigFiles)
            {
                try
                {
                    File.Delete(file);
                    System.Diagnostics.Debug.WriteLine(
                        $"[AccountManager] 已清除已迁移的旧配置: {Path.GetFileName(file)}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[AccountManager] 清除旧配置 {file} 失败: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine(
                $"[AccountManager] 迁移流程结束，共迁移 {migratedCount} 个账号");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 迁移过程发生错误: {ex.Message}");
        }
    }

    private async Task<string?> MigrateActiveAccountAsync()
    {
        try
        {
            var settings = App.GetService<ILocalSettingsService>();

            bool isInternationalAccount = false;
            try
            {
                var isOsObj = await settings.ReadSettingAsync("IsInternationalAccount");
                isInternationalAccount = isOsObj is bool b && b;
            }
            catch { }

            string mainConfigPath = isInternationalAccount
                ? Path.Combine(DataDir, "config.lab.json")
                : Path.Combine(DataDir, "config.json");

            if (!File.Exists(mainConfigPath))
                return null;

            var json = await File.ReadAllTextAsync(mainConfigPath);
            var config = JsonSerializer.Deserialize<Config>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (config?.Account == null || string.IsNullOrWhiteSpace(config.Account.Stuid))
                return null;

            string stuid = config.Account.Stuid;
            string serverType = isInternationalAccount ? "os" : "cn";
            string accountId = $"{serverType}_{stuid}";

            if (_accountList.Accounts.Any(a => a.Id == accountId))
            {
                await SetActiveAccountIdAsync(accountId);
                System.Diagnostics.Debug.WriteLine(
                    $"[AccountManager] 已迁移活跃账号: {accountId}");
                return mainConfigPath;
            }

            System.Diagnostics.Debug.WriteLine(
                $"[AccountManager] 旧活跃账号 {accountId} 不在迁移列表中，使用默认账号");
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AccountManager] 迁移活跃账号失败: {ex.Message}");
            return null;
        }
    }

    #endregion
}
