/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Protobuf;

namespace FufuLauncher.Models.GameServer;

public enum GameUpdateOperationKind
{
    Update,
    Predownload,
    ApplyPredownload,
}

public enum GameUpdateResult
{
    Completed,
    NothingToDo,
    Cancelled,
    Failed,
}

public sealed record GameUpdatePlan(
    GameUpdateOperationKind Kind,
    string TargetTag,
    long DownloadTotalBytes,
    long InstallTotalBytes,
    bool UsesPatchBuild);

public enum GameUpdateAssetOperationKind
{
    AddOrRepair,
    Modify,
    Delete,
}

public sealed class GameUpdateAssetOperation
{
    private GameUpdateAssetOperation(GameUpdateAssetOperationKind kind, string urlPrefix, string urlSuffix,
        AssetProperty? oldAsset, AssetProperty? newAsset, List<SophonChunk>? diffChunks)
    {
        Kind = kind;
        UrlPrefix = urlPrefix;
        UrlSuffix = urlSuffix;
        OldAsset = oldAsset;
        NewAsset = newAsset;
        DiffChunks = diffChunks ?? [];
    }

    public GameUpdateAssetOperationKind Kind { get; }
    public string UrlPrefix { get; }
    public string UrlSuffix { get; }
    public AssetProperty? OldAsset { get; }
    public AssetProperty? NewAsset { get; }
    public List<SophonChunk> DiffChunks { get; }
    
    public IEnumerable<SophonChunk> Chunks => Kind switch
    {
        GameUpdateAssetOperationKind.AddOrRepair => NewAsset!.AssetChunks.Select(chunk => new SophonChunk(UrlPrefix, UrlSuffix, chunk)),
        GameUpdateAssetOperationKind.Modify => DiffChunks,
        _ => [],
    };

    public static GameUpdateAssetOperation Add(string urlPrefix, string urlSuffix, AssetProperty newAsset)
    {
        return new GameUpdateAssetOperation(GameUpdateAssetOperationKind.AddOrRepair, urlPrefix, urlSuffix, null, newAsset, null);
    }

    public static GameUpdateAssetOperation Modify(string urlPrefix, string urlSuffix, AssetProperty oldAsset,
        AssetProperty newAsset, List<SophonChunk> diffChunks)
    {
        return new GameUpdateAssetOperation(GameUpdateAssetOperationKind.Modify, urlPrefix, urlSuffix, oldAsset, newAsset, diffChunks);
    }

    public static GameUpdateAssetOperation Delete(AssetProperty oldAsset)
    {
        return new GameUpdateAssetOperation(GameUpdateAssetOperationKind.Delete, string.Empty, string.Empty, oldAsset, null, null);
    }
}
