/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FufuLauncher.Models.MiHoYo.Fingerprint;
using FufuLauncher.Models.MiHoYo.Identity;
using FufuLauncher.Services.Device;
using FufuLauncher.Services.MiHoYo.Networking;

namespace FufuLauncher.Services.MiHoYo.Fingerprint;

public sealed class DeviceFpService
{
    private const string GetFpUrl = "https://public-data-api.mihoyo.com/device-fp/api/getFp";

    /// <summary><c>getFp</c> 的 <c>app_name</c>（国服）。</summary>
    public const string AppName = "bbs_cn";

    /// <summary><c>getFp</c> 的 <c>platform</c>（2 = 安卓 App）。</summary>
    public const string Platform = "2";

    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly MiHoYoDeviceStore _store;
    private readonly MobileDeviceService _device;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DeviceFpService(MiHoYoDeviceStore store, MobileDeviceService device)
    {
        _store = store;
        _device = device;
    }

    // ---------------------------------------------------------------- 取用


    public Task<MiHoYoDeviceIdentity> GetIdentityAsync(CancellationToken token = default) =>
        _store.GetOrCreateAsync(ResolveInitialDeviceId(), token);

    /// <summary>取用设备号（<c>x-rpc-device_id</c> 同源值）。</summary>
    public async Task<string> GetBbsDeviceIdAsync(CancellationToken token = default) =>
        (await GetIdentityAsync(token).ConfigureAwait(false)).BbsDeviceId;

    /// <summary>取用设备指纹；尚未注册时返回 null。</summary>
    public async Task<string?> GetFingerprintAsync(CancellationToken token = default)
    {
        var identity = await GetIdentityAsync(token).ConfigureAwait(false);
        return string.IsNullOrEmpty(identity.DeviceFp) ? null : identity.DeviceFp;
    }


    public async Task<MiHoYoDeviceIdentity> GetOrRegisterAsync(CancellationToken token = default)
    {
        var identity = await GetIdentityAsync(token).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(identity.DeviceFp))
        {
            return identity;
        }

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // 双检：等锁期间可能已被其它调用注册完成。
            identity = await GetIdentityAsync(token).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(identity.DeviceFp))
            {
                return identity;
            }

            string? issued = await RegisterAsync(identity, token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(issued))
            {
                Debug.WriteLine("[DeviceFp] 注册未获得指纹，返回无指纹身份");
                return identity;
            }

            // 只把指纹回填到发起注册时用的那个 device_id 上。
            // 若期间发生过重置，写入会被拒绝，避免旧指纹挂到新设备号。
            var updated = await _store.WithFingerprintAsync(identity.DeviceId, issued, token)
                .ConfigureAwait(false);
            if (updated is null)
            {
                Debug.WriteLine("[DeviceFp] 身份已重置，丢弃本次注册结果");
                return await GetIdentityAsync(token).ConfigureAwait(false);
            }

            Debug.WriteLine($"[DeviceFp] 注册成功并已回填: {issued}");
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<MiHoYoDeviceIdentity> ResetAndRegisterAsync(CancellationToken token = default)
    {
        await _store.ResetAsync(ResolveInitialDeviceId(), token).ConfigureAwait(false);
        return await GetOrRegisterAsync(token).ConfigureAwait(false);
    }

    public Task SaveIdentityAsync(MiHoYoDeviceIdentity identity, CancellationToken token = default) =>
        _store.SaveAsync(identity, token);


    public string BuildExtFieldsJson() => FpExtFieldsBuilder.BuildJson(_device.CaptureSnapshot());

    // ---------------------------------------------------------------- 内部

    private async Task<string?> RegisterAsync(MiHoYoDeviceIdentity identity, CancellationToken token)
    {
        try
        {
            var request = new DeviceFpRequest
            {
                DeviceId = identity.DeviceId,
                SeedId = identity.SeedId,
                SeedTime = identity.SeedTime,
                Platform = Platform,
                DeviceFp = CreatePlaceholderFingerprint(),
                AppName = AppName,
                ExtFields = FpExtFieldsBuilder.BuildJson(_device.CaptureSnapshot()),
                BbsDeviceId = identity.BbsDeviceId,
            };

            string bodyJson = JsonSerializer.Serialize(request, _jsonOptions);
            using var req = new HttpRequestMessage(HttpMethod.Post, GetFpUrl);
            req.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            MiHoYoHeaderFactory.ApplyDeviceFpHeaders(req);

            using var resp = await _httpClient.SendAsync(req, token).ConfigureAwait(false);
            string json = await resp.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            Debug.WriteLine($"[DeviceFp] getFp 状态码: {(int)resp.StatusCode}, 响应: {Truncate(json, 300)}");

            return ParseFingerprint(json);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DeviceFp] getFp 异常: {ex.Message}");
            return null;
        }
    }

    /// <summary>解析 <c>getFp</c> 响应中的 <c>device_fp</c>；任一步失败返回 null。</summary>
    private static string? ParseFingerprint(string json)
    {
        try
        {
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
                string? fp = fpProp.GetString();
                return string.IsNullOrEmpty(fp) ? null : fp;
            }

            Debug.WriteLine("[DeviceFp] getFp 响应未包含 device_fp");
            return null;
        }
        catch (JsonException ex)
        {
            Debug.WriteLine($"[DeviceFp] getFp 响应解析失败: {ex.Message}");
            return null;
        }
    }


    private string ResolveInitialDeviceId() => _device.Device.AndroidId;

    private static string CreatePlaceholderFingerprint()
    {
        Span<char> buffer = stackalloc char[10];
        buffer[0] = (char)('1' + Random.Shared.Next(9));
        for (int i = 1; i < buffer.Length; i++)
        {
            buffer[i] = (char)('0' + Random.Shared.Next(10));
        }

        return new string(buffer);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}