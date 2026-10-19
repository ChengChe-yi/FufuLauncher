/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Collections.Concurrent;
using Microsoft.Win32.SafeHandles;
using FufuLauncher.Helpers;
using FufuLauncher.Models.GameServer;
using FufuLauncher.Protobuf;

namespace FufuLauncher.Services.GameServer;

public sealed partial class GameUpdateService
{
    private async Task<List<AssetProperty>> VerifyAssetsAsync(GameUpdateContext context, SophonManifestProto manifest)
    {
        var brokenAssets = new ConcurrentBag<AssetProperty>();
        int totalAssets = manifest.Assets.Count;
        int doneAssets = 0;
        string stage = "GameServer_StageVerify".GetLocalized();

        await Parallel.ForEachAsync(manifest.Assets, context.ParallelOptions, async (asset, token) =>
        {
            token.ThrowIfCancellationRequested();

            string assetPath = context.GetGameFilePath(asset.AssetName);
            bool isBroken = !File.Exists(assetPath);

            if (!isBroken)
            {
                try
                {
                    using SafeFileHandle fileHandle = File.OpenHandle(assetPath, FileMode.Open, FileAccess.Read,
                        FileShare.ReadWrite, FileOptions.RandomAccess);

                    foreach (var chunk in asset.AssetChunks)
                    {
                        token.ThrowIfCancellationRequested();

                        byte[] buffer = new byte[checked((int)chunk.ChunkSizeDecompressed)];
                        await ReadExactlyAsync(fileHandle, buffer, chunk.ChunkOnFileOffset, token)
                            .ConfigureAwait(false);

                        string actualMd5 = HashUtility.Md5Bytes(buffer);
                        if (!actualMd5.Equals(chunk.ChunkDecompressedHashMd5 ?? string.Empty,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            isBroken = true;
                            break;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException
                                               or OverflowException or UnauthorizedAccessException)
                {
                    isBroken = true;
                }
            }

            if (isBroken)
            {
                brokenAssets.Add(asset);
            }

            int current = Interlocked.Increment(ref doneAssets);
            if (current % 100 == 0 || current == totalAssets)
            {
                context.Progress.Report(new GameServerConversionProgress(
                    string.Format("GameServer_VerifyProgress".GetLocalized(), current, totalAssets),
                    totalAssets, current, 0, 0, null));
            }
        }).ConfigureAwait(false);

        return brokenAssets.ToList();
    }

    private static async Task ReadExactlyAsync(SafeFileHandle handle, Memory<byte> buffer, long offset,
        CancellationToken token)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await RandomAccess.ReadAsync(handle, buffer[total..], offset + total, token)
                .ConfigureAwait(false);
            if (read <= 0)
            {
                throw new EndOfStreamException("文件末尾早于预期");
            }

            total += read;
        }
    }
}
