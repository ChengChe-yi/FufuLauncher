/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Encodings.Web;
using System.Text.Json;
using FufuLauncher.Models.MiHoYo.Fingerprint;

namespace FufuLauncher.Services;

public partial class AccountManager
{
    #region 设备指纹

    private async Task<DeviceFpRequest?> ReadFingerprintCoreAsync(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && TryGetPropertyIgnoreCase(root, "fingerprint", out var fpProp)
                && fpProp.ValueKind == JsonValueKind.Object)
            {
                return fpProp.Deserialize<DeviceFpRequest>();
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 读取 fingerprint 失败: {ex.Message}");
            return null;
        }
    }

    public async Task<DeviceFpRequest?> LoadFingerprintAsync(string accountId)
    {
        var entry = _accountList.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (entry is null || string.IsNullOrEmpty(entry.CookieFilePath))
            return null;

        var path = Path.Combine(CookiesDir, entry.CookieFilePath);
        if (!File.Exists(path))
            return null;

        return await ReadFingerprintCoreAsync(path);
    }

    public DeviceFpRequest? LoadFingerprint(string accountId)
    {
        var entry = _accountList.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (entry is null || string.IsNullOrEmpty(entry.CookieFilePath))
            return null;

        var path = Path.Combine(CookiesDir, entry.CookieFilePath);
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object
                && TryGetPropertyIgnoreCase(root, "fingerprint", out var fpProp)
                && fpProp.ValueKind == JsonValueKind.Object)
            {
                return fpProp.Deserialize<DeviceFpRequest>();
            }
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AccountManager] 读取 fingerprint 失败: {ex.Message}");
            return null;
        }
    }

    public async Task SaveFingerprintAsync(string accountId, DeviceFpRequest fp)
    {
        var entry = _accountList.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (entry is null || string.IsNullOrEmpty(entry.CookieFilePath))
            return;

        var path = Path.Combine(CookiesDir, entry.CookieFilePath);
        await _lock.WaitAsync();
        try
        {
            Dictionary<string, string>? cookies;
            try
            {
                cookies = await ReadCookieValuesAsync(path);
            }
            catch (FileNotFoundException)
            {
                cookies = new Dictionary<string, string>();
            }

            if (cookies is null)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountManager] 指纹持久化中止：cookie 文件解析失败 {entry.CookieFilePath}");
                return;
            }

            var file = new AccountCookieFile(cookies, fp);
            var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            await File.WriteAllTextAsync(path, json);
        }
        finally { _lock.Release(); }
    }

    #endregion
}
