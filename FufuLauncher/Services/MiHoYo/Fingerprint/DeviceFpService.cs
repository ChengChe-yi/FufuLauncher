/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FufuLauncher.Models.MiHoYo.Fingerprint;
using FufuLauncher.Services.MiHoYo.Networking;

namespace FufuLauncher.Services.MiHoYo.Fingerprint;

/// <summary>
/// 设备指纹注册 / 获取服务（bbs_cn 原生平台，getFp）。
/// <para>
/// 传入 accountId：先到对应账号 cookie 文件的 <c>fingerprint</c> 段查找已保存的指纹；
/// 已有则直接返回，没有则生成随机注册请求体完成注册，成功后写入 cookie 文件并返回供调用方使用。
/// </para>
/// </summary>
public sealed class DeviceFpService
{
    private const string GetFpUrl = "https://public-data-api.mihoyo.com/device-fp/api/getFp";
    private const string AppName = "bbs_cn";
    private const string Platform = "2";

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AccountManager _accountManager;

    /// <summary>per-account 串行化：同一账号的“读已存 → 注册 → 持久化”不允许并发，避免双注册。</summary>
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public DeviceFpService(AccountManager accountManager)
    {
        _accountManager = accountManager;
    }

    /// <summary>
    /// 获取账号画像中的 bbs_device_id（请求头 x-rpc-device_id 与注册体同源）。
    /// </summary>
    public string? GetCurrentDeviceId(string accountId) =>
        _accountManager.LoadFingerprint(accountId)?.BbsDeviceId;

    /// <summary>
    /// 获取账号指纹：cookie 文件已保存则直接返回；否则生成随机注册请求体注册，
    /// 成功后持久化到该账号 cookie 文件并返回。
    /// </summary>
    public async Task<string?> GetFingerprintAsync(string accountId)
    {
        var req = await GetFingerprintRequestAsync(accountId);
        return req?.DeviceFp;
    }

    /// <summary>
    /// 获取账号完整指纹请求体（含 device_id / bbs_device_id / seed 等）。
    /// 同一账号串行执行“读已存 → 注册 → 持久化”；注册失败返回 null。
    /// </summary>
    public async Task<DeviceFpRequest?> GetFingerprintRequestAsync(string accountId)
    {
        var sem = _locks.GetOrAdd(accountId, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync();
        try
        {
            var saved = await _accountManager.LoadFingerprintAsync(accountId);
            if (saved is not null && !string.IsNullOrEmpty(saved.DeviceFp))
            {
                Debug.WriteLine($"[DeviceFp] 命中已保存指纹: {saved.DeviceFp}");
                return saved;
            }

            var request = BuildNewRequest();
            string? fp = await RegisterAsync(request);
            if (string.IsNullOrEmpty(fp))
            {
                Debug.WriteLine("[DeviceFp] 注册失败，未获得指纹");
                return null;
            }

            var persisted = request with { DeviceFp = fp };
            await _accountManager.SaveFingerprintAsync(accountId, persisted);
            Debug.WriteLine($"[DeviceFp] 注册成功并已持久化: {fp}");
            return persisted;
        }
        finally
        {
            sem.Release();
        }
    }

    /// <summary>
    /// 全新注册请求体：device_id / seed 随机生成，画像字段固定（ExtFields 模型默认值），
    /// bbs_device_id = nameUUIDFromBytes(device_id)（v3），与请求头 x-rpc-device_id 同源。
    /// 账号级稳定由持久化保证（首次注册后完整请求体存档复用）。
    /// </summary>
    private static DeviceFpRequest BuildNewRequest()
    {
        // appInstallTimeDiff/appUpdateTimeDiff 实为 PackageInfo.firstInstallTime/lastUpdateTime 绝对毫秒时间戳：
        // 安装晚于设备固件 buildTime（1779448087000），早于或等于当前时间；更新在安装之后。
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long firstInstall = now - Random.Shared.NextInt64(30, 120) * 86_400_000L;
        long lastUpdate = firstInstall + Random.Shared.NextInt64(0, 30) * 86_400_000L;
        if (lastUpdate > now)
            lastUpdate = now;

        // device_id 首次随机 16 hex（对应 AndroidID 角色），持久化后固定复用；
        // bbs_device_id = nameUUIDFromBytes(device_id)（v3），与原版算法一致。
        var deviceId = GenerateRandomHex(16);

        var extFields = new ExtFields
        {
            AppInstallTimeDiff = firstInstall,
            AppUpdateTimeDiff = lastUpdate,
        };

        return new DeviceFpRequest
        {
            DeviceId = deviceId,
            SeedId = Guid.NewGuid().ToString(),
            SeedTime = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            Platform = Platform,
            DeviceFp = GenerateDefaultDeviceId(),
            AppName = AppName,
            ExtFields = JsonSerializer.Serialize(extFields, _jsonOptions),
            BbsDeviceId = NameUuidFromBytes(Encoding.UTF8.GetBytes(deviceId)).ToString(),
        };
    }

    /// <summary>调用 getFp 注册，返回服务端下发的 device_fp；失败返回 null。</summary>
    private static async Task<string?> RegisterAsync(DeviceFpRequest request)
    {
        try
        {
            var bodyJson = JsonSerializer.Serialize(request, _jsonOptions);
            using var req = new HttpRequestMessage(HttpMethod.Post, GetFpUrl);
            req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            MiHoYoHeaderFactory.ApplyDeviceFpHeaders(req);

            using var resp = await _httpClient.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            Debug.WriteLine($"[DeviceFp] getFp 状态码: {(int)resp.StatusCode}, 响应: {Truncate(json, 300)}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("retcode", out var rc) && rc.GetInt32() != 0)
            {
                Debug.WriteLine($"[DeviceFp] getFp retcode={rc.GetInt32()}");
                return null;
            }

            if (root.TryGetProperty("data", out var data)
                && data.TryGetProperty("device_fp", out var fpProp))
            {
                var fp = fpProp.GetString();
                return string.IsNullOrEmpty(fp) ? null : fp;
            }

            Debug.WriteLine("[DeviceFp] getFp 响应未包含 device_fp");
            return null;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeviceFp] getFp 异常: {ex.Message}");
            return null;
        }
    }


    /// <summary>原版 SDK DeviceFingerprintSharedPreferences.random(10)：10 位十进制数字，首位 1-9。</summary>
    private static string GenerateDefaultDeviceId()
    {
        var rng = Random.Shared;
        return new string(new[] { (char)('1' + rng.Next(9)) }
            .Concat(Enumerable.Range(0, 9).Select(_ => (char)('0' + rng.Next(10)))).ToArray());
    }

    private static string GenerateRandomHex(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes((length + 1) / 2);
        return Convert.ToHexString(bytes).ToLowerInvariant()[..length];
    }

    /// <summary>Java <c>UUID.nameUUIDFromBytes</c> 等价实现（v3：MD5 + 版本位 / 变体位）。</summary>
    private static Guid NameUuidFromBytes(byte[] name)
    {
        var hash = MD5.HashData(name);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        Array.Reverse(hash, 0, 4);
        Array.Reverse(hash, 4, 2);
        Array.Reverse(hash, 6, 2);
        return new Guid(hash);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
