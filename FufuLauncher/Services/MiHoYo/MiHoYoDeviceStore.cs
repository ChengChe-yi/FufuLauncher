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

    private static int _writeSequence;

    public async Task<MiHoYoDeviceIdentity?> LoadAsync(CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            return await ReadCoreAsync(token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }


    public async Task SaveAsync(MiHoYoDeviceIdentity identity, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await WriteCoreAsync(identity, token).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }


    public async Task<MiHoYoDeviceIdentity> GetOrCreateAsync(
        string? initialDeviceId = null, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var current = await ReadCoreAsync(token).ConfigureAwait(false);
            if (current is not null && current.IsUsable)
            {
                return current;
            }

            var created = MiHoYoDeviceIdentity.CreateNew(initialDeviceId);
            await WriteCoreAsync(created, token).ConfigureAwait(false);
            Debug.WriteLine($"[MiHoYoDevice] 已生成并持久化 device_id={created.DeviceId}");
            return created;
        }
        finally
        {
            _gate.Release();
        }
    }


    public async Task<MiHoYoDeviceIdentity?> WithFingerprintAsync(
        string expectedDeviceId, string deviceFp, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var current = await ReadCoreAsync(token).ConfigureAwait(false);
            if (current is null
                || !current.IsUsable
                || !string.Equals(current.DeviceId, expectedDeviceId, StringComparison.Ordinal))
            {
                Debug.WriteLine(
                    $"[MiHoYoDevice] 身份已变更（期望 {expectedDeviceId}），丢弃过期指纹");
                return null;
            }

            var updated = current with { DeviceFp = deviceFp };
            await WriteCoreAsync(updated, token).ConfigureAwait(false);
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }


    public async Task<MiHoYoDeviceIdentity> ResetAsync(
        string? initialDeviceId = null, CancellationToken token = default)
    {
        await _gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var created = MiHoYoDeviceIdentity.CreateNew(initialDeviceId);
            await WriteCoreAsync(created, token).ConfigureAwait(false);
            Debug.WriteLine($"[MiHoYoDevice] 已重置并持久化 device_id={created.DeviceId}");
            return created;
        }
        finally
        {
            _gate.Release();
        }
    }

    // ---------------------------------------------------------------- 无锁内核（仅在持锁期间调用）

    private async Task<MiHoYoDeviceIdentity?> ReadCoreAsync(CancellationToken token)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        string path = AppPaths.MiHoYoDeviceFile;
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(path, token).ConfigureAwait(false);
            var loaded = JsonSerializer.Deserialize<MiHoYoDeviceIdentity>(json, JsonOptions);
            if (loaded is null)
            {
                // 空/字面 null：内容不可用，允许后续替换。
                Debug.WriteLine("[MiHoYoDevice] 文件内容为空，视为不可用");
                return null;
            }

            _cached = loaded;
            return loaded;
        }
        catch (JsonException ex)
        {
            // 内容损坏：不可用，允许后续替换（保留文件由调用方决定）。
            Debug.WriteLine($"[MiHoYoDevice] 内容解析失败，视为不可用: {ex.Message}");
            return null;
        }
        // IO / 权限异常不在此处吞掉：文件可能完好，覆盖会丢失设备身份。
    }


    private async Task WriteCoreAsync(MiHoYoDeviceIdentity identity, CancellationToken token)
    {
        string path = AppPaths.MiHoYoDeviceFile;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        string temporary = $"{path}.{Environment.ProcessId}.{Interlocked.Increment(ref _writeSequence)}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporary, JsonSerializer.Serialize(identity, JsonOptions), token).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            TryDelete(temporary);
            throw;
        }

        // 仅在确实落盘后才更新缓存，避免把未持久化的值当作已保存。
        _cached = identity;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Debug.WriteLine($"[MiHoYoDevice] 清理临时文件失败 {path}: {ex.Message}");
        }
    }
}