/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.

从 Yae CDN 获取成就元数据（AchievementInfo protobuf），
支持三镜像下载、ETag 缓存、GIF8 混淆解密与游戏哈希解析。
镜像与协议参考 HolographicHat/YaeAchievement。
*/
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using FufuLauncher.Helpers;
using FufuLauncher.Services.Yae.Proto;

namespace FufuLauncher.Services.Yae;

public static class YaeMetadataService
{
    private static readonly string[] Mirrors =
    [
        "https://rin.holohat.work/schicksal/metadata",
        "https://ena-rin.holohat.work//schicksal/metadata",
        "https://cn-cd-1259389942.file.myqcloud.com/schicksal/metadata",
    ];

    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30),
    };

    /// <summary>
    /// 获取并解析 Yae 成就元数据。使用本地 ETag 缓存，若 CDN 不可用则回退到缓存。
    /// </summary>
    public static async Task<AchievementInfo> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        var cacheDir = Path.Combine(AppPaths.CacheDir, "Yae");
        Directory.CreateDirectory(cacheDir);
        var binPath = Path.Combine(cacheDir, "metadata.bin");
        var etagPath = Path.Combine(cacheDir, "metadata.etag");

        string? lastError = null;
        foreach (var mirror in Mirrors)
        {
            try
            {
                var raw = await FetchWithEtagAsync(mirror, binPath, etagPath, cancellationToken).ConfigureAwait(false);
                var data = DecryptIfNeeded(raw);
                return AchievementInfo.Parser.ParseFrom(data);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or SocketException or InvalidDataException)
            {
                lastError = ex.Message;
            }
        }

        // 所有镜像均失败时，尝试使用本地缓存（若已存在且可解析）。
        if (File.Exists(binPath))
        {
            try
            {
                var data = DecryptIfNeeded(File.ReadAllBytes(binPath));
                return AchievementInfo.Parser.ParseFrom(data);
            }
            catch (Exception) { /* ignore */ }
        }

        throw new ApplicationException(lastError ?? "无法获取 Yae 成就元数据，请检查网络连接。");
    }

    /// <summary>
    /// 计算游戏进程哈希：CRC-32 对 exe 前 0x10000 字节。
    /// </summary>
    public static uint ComputeGameHash(string exePath)
    {
        try
        {
            Span<byte> buffer = stackalloc byte[0x10000];
            using var stream = File.OpenRead(exePath);
            _ = stream.ReadAtLeast(buffer, 0x10000, false);
            return YaeCrc32.Compute(buffer);
        }
        catch (IOException)
        {
            return 0xFFFFFFFF;
        }
    }

    /// <summary>
    /// 根据游戏哈希解析目标版本的命令 ID 与 RVA 配置。
    /// </summary>
    public static YaeNativeConfiguration Resolve(AchievementInfo metadata, uint gameHash)
    {
        if (!metadata.NativeConfig.MethodRva.TryGetValue(gameHash, out var rva))
        {
            throw new ApplicationException($"未找到该游戏版本对应的 Yae 配置（哈希 0x{gameHash:X8}），请更新游戏版本或等待元数据更新。");
        }

        return new YaeNativeConfiguration
        {
            StoreCmdId = metadata.NativeConfig.StoreCmdId,
            AchievementCmdId = metadata.NativeConfig.AchievementCmdId,
            DoCmd = rva.DoCmd,
            UpdateNormalProperty = rva.UpdateNormalProp,
            NewString = rva.NewString,
            FindGameObject = rva.FindGameObject,
            EventSystemUpdate = rva.EventSystemUpdate,
            SimulatePointerClick = rva.SimulatePointerClick,
            ToInt32 = rva.ToInt32,
            TcpStatePtr = rva.TcpStatePtr,
            SharedInfoPtr = rva.SharedInfoPtr,
            Decompress = rva.Decompress,
        };
    }

    private static async Task<byte[]> FetchWithEtagAsync(string url, string binPath, string etagPath, CancellationToken cancellationToken)
    {
        byte[] cached = File.Exists(binPath) ? await File.ReadAllBytesAsync(binPath, cancellationToken).ConfigureAwait(false) : [];

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (File.Exists(etagPath))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", await File.ReadAllTextAsync(etagPath, cancellationToken).ConfigureAwait(false));
        }

        using var response = await HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotModified && cached.Length > 0)
        {
            return cached;
        }

        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        if (response.Headers.ETag?.Tag is { } etag)
        {
            await File.WriteAllTextAsync(etagPath, etag, cancellationToken).ConfigureAwait(false);
        }
        await File.WriteAllBytesAsync(binPath, bytes, cancellationToken).ConfigureAwait(false);
        return bytes;
    }

    /// <summary>
    /// Yae CDN 可能返回 GIF8 混淆的数据：异或 + Brotli 解压 + CRC 校验。
    /// 普通 protobuf 响应原样返回。
    /// </summary>
    private static byte[] DecryptIfNeeded(byte[] data)
    {
        if (data.Length < 52 || BitConverter.ToUInt32(data, 0) != 0x38464947) // "GIF8"
        {
            return data;
        }

        var seed = BitConverter.ToUInt32(data, 44) ^ 0x01919810;
        var hush = BitConverter.ToUInt32(data, 48) - 0x32123432;
        var span = data.AsSpan()[52..];

        var xorTable = new byte[4096];
        new Random(unchecked((int)seed)).NextBytes(xorTable);
        for (var i = 0; i < span.Length; i++)
        {
            span[i] ^= xorTable[i % 4096];
        }

        using var output = new MemoryStream();
        using (var compressed = new MemoryStream(span.ToArray()))
        using (var decompressor = new BrotliStream(compressed, CompressionMode.Decompress))
        {
            decompressor.CopyTo(output);
        }
        var result = output.ToArray();
        if (YaeCrc32.Compute(result) != hush)
        {
            throw new InvalidDataException("Yae 元数据解密校验失败。");
        }
        return result;
    }
}
