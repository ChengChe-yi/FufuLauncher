/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Models;
using FufuLauncher.Models.MiHoYo.Fingerprint;

namespace FufuLauncher.Services;

public sealed record AccountCookieFile(
    [property: System.Text.Json.Serialization.JsonPropertyName("cookies")]
    Dictionary<string, string> Cookies,
    [property: System.Text.Json.Serialization.JsonPropertyName("fingerprint")]
    DeviceFpRequest? Fingerprint = null);

public partial class AccountManager
{
    private string DataDir => Helpers.AppPaths.DataDir;
    private string CookiesDir => Path.Combine(DataDir, "cookies");
    private string AccountsFilePath => Path.Combine(DataDir, "accounts.json");
    private readonly SemaphoreSlim _lock = new(1, 1);
    private const int CookieFileVersion = 1;

    private AccountList _accountList;
    private string? _activeAccountId;
    public string? ActiveAccountId => _activeAccountId;
    public AccountManager()
    {
        try
        {
            Directory.CreateDirectory(CookiesDir);
        }
        catch (UnauthorizedAccessException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 无法创建 cookies 目录: {ex.Message}");
        }
        catch (IOException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 创建 cookies 目录时发生IO异常: {ex.Message}");
        }
        _accountList = new AccountList();
    }

    public async Task InitializeAsync()
    {
        await LoadAccountListAsync();

        if (HasLegacyAccounts())
        {
            await MigrateLegacyAccountsAsync();
        }
    }

    public AccountEntry GetActiveAccountEntry() =>
        _accountList.Accounts.FirstOrDefault(a => a.Id == _activeAccountId);

    public List<AccountEntry> GetAllAccounts() => _accountList.Accounts;

    private async Task LoadAccountListAsync()
    {
        if (File.Exists(AccountsFilePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(AccountsFilePath);
                _accountList = JsonSerializer.Deserialize<AccountList>(json) ?? new AccountList();
            }
            catch (JsonException ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AccountManager] accounts.json 解析失败，将重置账号列表: {ex.Message}");
                try
                {
                    var backupPath = AccountsFilePath + $".corrupt.{DateTime.Now:yyyyMMddHHmmss}.bak";
                    File.Copy(AccountsFilePath, backupPath, overwrite: true);
                }
                catch { }
                _accountList = new AccountList();
            }
        }
        else
        {
            _accountList = new AccountList();
        }

        var normalizedAccounts = _accountList.Accounts
            .Where(a => !string.IsNullOrWhiteSpace(a?.Id))
            .GroupBy(a => a.Id)
            .Select(g => g.Last())
            .ToList();

        bool metadataChanged = false;
        foreach (var account in normalizedAccounts)
        {
            if (account.CookieVersion <= 0)
            {
                bool migrated = await MigrateLegacyCookieFileAsync(account);
                if (migrated)
                {
                    account.CookieVersion = CookieFileVersion;
                    metadataChanged = true;
                }
            }
            if (account.UpdatedAt == default)
            {
                account.UpdatedAt = account.LastLoginTime == default ? DateTime.Now : account.LastLoginTime;
                metadataChanged = true;
            }
        }

        if (normalizedAccounts.Count != _accountList.Accounts.Count || metadataChanged)
        {
            _accountList.Accounts = normalizedAccounts;
            await SaveAccountListAsync();
        }
        else
        {
            _accountList.Accounts = normalizedAccounts;
        }

        var settings = App.GetService<ILocalSettingsService>();
        try
        {
            var savedObj = await settings.ReadSettingAsync("ActiveAccountId");
            var savedId = savedObj as string;
            _activeAccountId = savedId ?? _accountList.Accounts.FirstOrDefault()?.Id;
        }
        catch
        {
            _activeAccountId = _accountList.Accounts.FirstOrDefault()?.Id;
        }
    }

    public async Task SetActiveAccountIdAsync(string? accountId)
    {
        _activeAccountId = accountId;
        var settings = App.GetService<ILocalSettingsService>();
        if (settings != null)
            await settings.SaveSettingAsync("ActiveAccountId", accountId ?? string.Empty);
    }

    public async Task LogoutAsync()
    {
        await SetActiveAccountIdAsync(null);
    }

    private async Task SaveAccountListAsync()
    {
        var json = JsonSerializer.Serialize(_accountList, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(AccountsFilePath, json);
    }
}
