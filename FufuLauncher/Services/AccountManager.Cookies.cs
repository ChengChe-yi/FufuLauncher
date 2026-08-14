/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Encodings.Web;
using System.Text.Json;

namespace FufuLauncher.Services;

public partial class AccountManager
{
    #region Cookie 文件读写

    private async Task WriteCookieFileAsync(string path, Dictionary<string, string> cookies)
    {
        var file = new AccountCookieFile(cookies, await ReadFingerprintCoreAsync(path));
        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        await File.WriteAllTextAsync(path, json);
    }

    private async Task<Dictionary<string, string>?> ReadCookieValuesAsync(string path)
    {
        try
        {
            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json);

            if (TryGetPropertyIgnoreCase(root, "cookies", out var cookiesProp)
                && cookiesProp.ValueKind == JsonValueKind.Object)
            {
                return ReadStringDictionary(cookiesProp);
            }

            if (TryGetPropertyIgnoreCase(root, "values", out var valuesProp)
                && valuesProp.ValueKind == JsonValueKind.Object)
            {
                return ReadStringDictionary(valuesProp);
            }

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[AccountManager] Cookie 文件解析失败: {ex.Message}");
            return null;
        }
    }

    public async Task<Dictionary<string, string>> LoadCookiesAsync(string accountId)
    {
        var entry = _accountList.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (entry == null) return null;

        string path = Path.Combine(CookiesDir, entry.CookieFilePath);
        if (!File.Exists(path)) return null;

        return await ReadCookieValuesAsync(path);
    }

    public async Task UpdateCookiesAsync(string accountId, Dictionary<string, string> newCookies)
    {
        await _lock.WaitAsync();
        try
        {
            var entry = _accountList.Accounts.FirstOrDefault(a => a.Id == accountId);
            if (entry == null) return;

            string cookiePath = Path.Combine(CookiesDir, entry.CookieFilePath);
            await WriteCookieFileAsync(cookiePath, newCookies);
            entry.CookieVersion = CookieFileVersion;
            entry.UpdatedAt = DateTime.Now;
            await SaveAccountListAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    #endregion
}
