/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using FufuLauncher.Helpers;
using FufuLauncher.Models.MiHoYo.Identity;

namespace FufuLauncher.Services.MiHoYo;


public sealed class MiHoYoDeviceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private MiHoYoDeviceIdentity? _cached;

    /// <summary>读取已保存的身份；不存在或损坏时返回 null（不写盘）。</summary>
    public async Task<MiHoYoDeviceIdentity?> LoadAsync(CancellationToken token = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _cached ??= await TryReadAsync(token).ConfigureAwait(false);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>保存身份（原子写：先写临时文件再替换）。</summary>
    public async Task SaveAsync(MiHoYoDeviceIdentity identity, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await WriteAsync(identity, token).ConfigureAwait(false);
            _cached = identity;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>读取身份；不存在或不可用时生成一份并落盘。</summary>
    public async Task<MiHoYoDeviceIdentity> GetOrCreateAsync(CancellationToken token = default)
    {
        var loaded = await LoadAsync(token).ConfigureAwait(false);
        if (loaded is not null && loaded.IsUsable)
        {
            return loaded;
        }

        var created = MiHoYoDeviceIdentity.CreateNew();
        await SaveAsync(created, token).ConfigureAwait(false);
        Debug.WriteLine($"[MiHoYoDevice] 已生成并持久化 device_id={created.DeviceId}");
        return created;
    }

    /// <summary>回填服务端签发的设备指纹（保留 device_id / seed）。</summary>
    public async Task<MiHoYoDeviceIdentity> WithFingerprintAsync(
        string deviceFp, CancellationToken token = default)
    {
        var current = await GetOrCreateAsync(token).ConfigureAwait(false);
        var updated = current with { DeviceFp = deviceFp };
        await SaveAsync(updated, token).ConfigureAwait(false);
        return updated;
    }

    /// <summary>丢弃现有身份并重新生成。</summary>
    public async Task<MiHoYoDeviceIdentity> ResetAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            _cached = null;
        }
        finally
        {
            _gate.Release();
        }

        return await GetOrCreateAsync(token).ConfigureAwait(false);
    }

    private static async Task<MiHoYoDeviceIdentity?> TryReadAsync(CancellationToken token)
    {
        try
        {
            string path = AppPaths.MiHoYoDeviceFile;
            if (!File.Exists(path))
            {
                return null;
            }

            string json = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
            return JsonSerializer.Deserialize<MiHoYoDeviceIdentity>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[MiHoYoDevice] 读取失败，将重新生成: {ex.Message}");
            return null;
        }
    }

    private static async Task WriteAsync(MiHoYoDeviceIdentity identity, CancellationToken token)
    {
        try
        {
            string path = AppPaths.MiHoYoDeviceFile;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            // 先写临时文件再替换，避免写入中断留下半截 JSON 导致身份丢失。
            string temporary = path + ".tmp";
            await File.WriteAllTextAsync(
                temporary, JsonSerializer.Serialize(identity, JsonOptions), token).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[MiHoYoDevice] 持久化失败，本次仍使用内存值: {ex.Message}");
        }
    }
}
