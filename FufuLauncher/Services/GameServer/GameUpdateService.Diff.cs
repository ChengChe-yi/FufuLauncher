/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Models.GameServer;
using FufuLauncher.Protobuf;

namespace FufuLauncher.Services.GameServer;

public sealed partial class GameUpdateService
{
    private static List<GameUpdateAssetOperation> BuildDiffOperations(SophonManifestProto localManifest,
        SophonManifestProto targetManifest,
        string urlPrefix, string urlSuffix)
    {
        var operations = new List<GameUpdateAssetOperation>();
        var localMap = localManifest.Assets.ToDictionary(asset => asset.AssetName, StringComparer.OrdinalIgnoreCase);

        foreach (var targetAsset in targetManifest.Assets)
        {
            if (!localMap.TryGetValue(targetAsset.AssetName, out var localAsset))
            {
                operations.Add(GameUpdateAssetOperation.Add(urlPrefix, urlSuffix, targetAsset));
                continue;
            }

            if ((localAsset.AssetHashMd5 ?? string.Empty).Equals(targetAsset.AssetHashMd5 ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            List<SophonChunk> diffChunks = BuildDiffChunks(localAsset, targetAsset, urlPrefix, urlSuffix);
            operations.Add(GameUpdateAssetOperation.Modify(urlPrefix, urlSuffix, localAsset, targetAsset, diffChunks));
        }

        var targetNames = targetManifest.Assets.Select(asset => asset.AssetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var localAsset in localManifest.Assets)
        {
            if (!targetNames.Contains(localAsset.AssetName))
            {
                operations.Add(GameUpdateAssetOperation.Delete(localAsset));
            }
        }

        return operations;
    }

    private static List<SophonChunk> BuildDiffChunks(AssetProperty localAsset, AssetProperty targetAsset,
        string urlPrefix, string urlSuffix)
    {
        return targetAsset.AssetChunks
            .Where(chunk => localAsset.AssetChunks.FirstOrDefault(candidate =>
                (candidate.ChunkDecompressedHashMd5 ?? string.Empty).Equals(
                    chunk.ChunkDecompressedHashMd5 ?? string.Empty, StringComparison.OrdinalIgnoreCase)) is null)
            .Select(chunk => new SophonChunk(urlPrefix, urlSuffix, chunk))
            .ToList();
    }

    private static void InitializeDuplicatedChunkNames(GameUpdateContext context,
        IEnumerable<GameUpdateAssetOperation> operations)
    {
        IEnumerable<string> names = operations
            .SelectMany(operation => operation.Chunks)
            .Select(chunk => chunk.AssetChunk.ChunkName)
            .GroupBy(name => name, StringComparer.Ordinal)
            .Where(group => group.Skip(1).Any())
            .Select(group => group.Key);

        foreach (string name in names)
        {
            context.DuplicatedChunkNames.TryAdd(name, 0);
        }
    }
}