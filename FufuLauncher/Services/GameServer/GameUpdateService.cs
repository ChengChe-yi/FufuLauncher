/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json;
using FufuLauncher.Constants;
using FufuLauncher.Helpers;
using FufuLauncher.Models.GameServer;
using FufuLauncher.Protobuf;

namespace FufuLauncher.Services.GameServer;

public sealed partial class GameUpdateService
{
    public const string ChunksDirectoryName = "chunks";

    public const string PredownloadStatusFileName = "predownload_status.json";

    private const long TempFileMarginBytes = 1024L * 1024L * 1024L;

    private readonly SophonBuildClient _sophonBuildClient;
    private readonly ChunkDownloader _chunkDownloader;
    private readonly GameServerConfigurationService _configurationService;
    private readonly GameChannelSdkService _gameChannelSdkService;

    public GameUpdateService(SophonBuildClient sophonBuildClient, ChunkDownloader chunkDownloader,
        GameServerConfigurationService configurationService, GameChannelSdkService gameChannelSdkService)
    {
        _sophonBuildClient = sophonBuildClient;
        _chunkDownloader = chunkDownloader;
        _configurationService = configurationService;
        _gameChannelSdkService = gameChannelSdkService;
    }

    #region 公开入口

    public async Task<GameUpdateResult> PredownloadAsync(string gameDir, GameServerScheme currentScheme,
        IProgress<GameServerConversionProgress> progress, Action<string> log, CancellationToken token = default,
        GameServerDownloadMonitor? downloadMonitor = null, Func<GameUpdatePlan, Task<bool>>? confirmAsync = null)
    {
        ArgumentNullException.ThrowIfNull(currentScheme);

        EnsureDirectoryPermissions(gameDir);

        var context = new GameUpdateContext(gameDir, currentScheme, progress, log, token, downloadMonitor);
        string? localVersion = _configurationService.TryGetGameVersion(gameDir);
        if (string.IsNullOrEmpty(localVersion))
        {
            log("GameUpdate_UnknownLocalVersion".GetLocalized());
            return GameUpdateResult.Failed;
        }

        log("GameServer_StageFetchBranches".GetLocalized());
        SophonBranchPayload preBranch = await _sophonBuildClient.GetBranchPayloadAsync(currentScheme, true, token)
            .ConfigureAwait(false);
        if (string.Equals(preBranch.Tag, localVersion, StringComparison.Ordinal))
        {
            log("GameUpdate_NoPredownloadNeeded".GetLocalized());
            return GameUpdateResult.NothingToDo;
        }

        SophonBranchInfo localInfo = await _sophonBuildClient
            .GetBranchInfoByTagAsync(currentScheme, localVersion, token).ConfigureAwait(false);
        SophonBranchInfo preInfo =
            await _sophonBuildClient.GetBranchInfoAsync(currentScheme, true, token).ConfigureAwait(false);

        log("GameServer_StageDecodeManifests".GetLocalized());
        SophonManifestProto localManifest =
            await _sophonBuildClient.DownloadManifestAsync(localInfo, token).ConfigureAwait(false);
        SophonManifestProto preManifest =
            await _sophonBuildClient.DownloadManifestAsync(preInfo, token).ConfigureAwait(false);

        log("GameServer_StageDiff".GetLocalized());
        List<GameUpdateAssetOperation> operations =
            BuildDiffOperations(localManifest, preManifest, preInfo.ChunkPrefix, preInfo.ChunkSuffix);
        List<GameUpdateAssetOperation> downloadOperations = operations
            .Where(operation => operation.Kind != GameUpdateAssetOperationKind.Delete)
            .ToList();
        SophonDecodedPatchBuild? patchBuild =
            await TryDecodePatchBuildAsync(currentScheme, preBranch, localVersion, token).ConfigureAwait(false);
        List<SophonPatchAsset> patchAssets = patchBuild is null ? [] : EnumeratePatchAssets(patchBuild);

        long downloadTotalBytes;
        int totalBlocks;
        if (patchAssets.Count > 0)
        {
            downloadTotalBytes = patchAssets.Sum(asset => asset.PatchInfo.PatchFileSize);
            totalBlocks = patchAssets.Select(asset => asset.PatchInfo.Id).Distinct(StringComparer.Ordinal).Count();
        }
        else
        {
            downloadTotalBytes =
                downloadOperations.Sum(operation => operation.Chunks.Sum(chunk => chunk.AssetChunk.ChunkSize));
            totalBlocks = downloadOperations.SelectMany(operation => operation.Chunks)
                .Select(chunk => chunk.AssetChunk.ChunkName)
                .Distinct(StringComparer.Ordinal)
                .Count();
        }

        if (totalBlocks == 0)
        {
            log("GameUpdate_NoPredownloadNeeded".GetLocalized());
            return GameUpdateResult.NothingToDo;
        }

        EnsureFreeSpace(context, downloadTotalBytes);

        var plan = new GameUpdatePlan(GameUpdateOperationKind.Predownload, preBranch.Tag, downloadTotalBytes,
            downloadTotalBytes, patchAssets.Count > 0);
        if (confirmAsync is not null && !await confirmAsync(plan).ConfigureAwait(false))
        {
            log("AdvancedServerSwitch_Cancelled".GetLocalized());
            return GameUpdateResult.Cancelled;
        }

        log(string.Format("GameUpdate_PlanSummary".GetLocalized(), ToSizeString(downloadTotalBytes), preBranch.Tag));

        Directory.CreateDirectory(context.ChunksDir);

        PredownloadStatus status = new() { Tag = preBranch.Tag, Finished = false, TotalBlocks = totalBlocks };
        await WritePredownloadStatusAsync(context, status, token).ConfigureAwait(false);

        if (patchAssets.Count > 0)
        {
            await PredownloadPatchesAsync(context, patchAssets).ConfigureAwait(false);
        }
        else
        {
            await DownloadOperationChunksAsync(context, downloadOperations,
                "GameServer_StageDownloadChunks".GetLocalized()).ConfigureAwait(false);
        }

        status.Finished = true;
        await WritePredownloadStatusAsync(context, status, token).ConfigureAwait(false);
        log("GameUpdate_PredownloadDone".GetLocalized());

        return GameUpdateResult.Completed;
    }

    public async Task<GameUpdateResult> UpdateAsync(string gameDir, GameServerScheme currentScheme,
        bool usePredownloadAsTarget,
        IProgress<GameServerConversionProgress> progress, Action<string> log, CancellationToken token = default,
        GameServerDownloadMonitor? downloadMonitor = null, Func<GameUpdatePlan, Task<bool>>? confirmAsync = null)
    {
        ArgumentNullException.ThrowIfNull(currentScheme);

        EnsureDirectoryPermissions(gameDir);

        var context = new GameUpdateContext(gameDir, currentScheme, progress, log, token, downloadMonitor);
        string? localVersion = _configurationService.TryGetGameVersion(gameDir);
        if (string.IsNullOrEmpty(localVersion))
        {
            log("GameUpdate_UnknownLocalVersion".GetLocalized());
            return GameUpdateResult.Failed;
        }

        log("GameServer_StageFetchBranches".GetLocalized());
        SophonBranchPayload targetBranch = await _sophonBuildClient
            .GetBranchPayloadAsync(currentScheme, usePredownloadAsTarget, token).ConfigureAwait(false);
        if (string.Equals(targetBranch.Tag, localVersion, StringComparison.Ordinal))
        {
            log("GameUpdate_AlreadyLatest".GetLocalized());
            return GameUpdateResult.NothingToDo;
        }

        SophonBranchInfo localInfo = await _sophonBuildClient
            .GetBranchInfoByTagAsync(currentScheme, localVersion, token).ConfigureAwait(false);
        SophonBranchInfo targetInfo = await _sophonBuildClient
            .GetBranchInfoAsync(currentScheme, usePredownloadAsTarget, token).ConfigureAwait(false);

        log("GameServer_StageDecodeManifests".GetLocalized());
        SophonManifestProto localManifest =
            await _sophonBuildClient.DownloadManifestAsync(localInfo, token).ConfigureAwait(false);
        SophonManifestProto targetManifest =
            await _sophonBuildClient.DownloadManifestAsync(targetInfo, token).ConfigureAwait(false);

        log("GameServer_StageDiff".GetLocalized());
        List<GameUpdateAssetOperation> operations = BuildDiffOperations(localManifest, targetManifest,
            targetInfo.ChunkPrefix, targetInfo.ChunkSuffix);

        SophonDecodedPatchBuild? patchBuild =
            await TryDecodePatchBuildAsync(currentScheme, targetBranch, localVersion, token).ConfigureAwait(false);
        List<SophonPatchAsset> patchAssets = patchBuild is null ? [] : EnumeratePatchAssets(patchBuild);

        long downloadTotalBytes = patchAssets.Count > 0
            ? patchAssets.Sum(asset => asset.PatchInfo.PatchFileSize)
            : operations.Sum(operation => operation.Chunks.Sum(chunk => chunk.AssetChunk.ChunkSize));
        long installTotalBytes = patchBuild?.UncompressedTotalBytes
                                 ?? operations.Where(operation => operation.Kind != GameUpdateAssetOperationKind.Delete)
                                     .Sum(operation => operation.NewAsset!.AssetSize);

        EnsureFreeSpace(context, installTotalBytes);

        var plan = new GameUpdatePlan(
            usePredownloadAsTarget ? GameUpdateOperationKind.ApplyPredownload : GameUpdateOperationKind.Update,
            targetBranch.Tag, downloadTotalBytes, installTotalBytes, patchAssets.Count > 0);
        if (confirmAsync is not null && !await confirmAsync(plan).ConfigureAwait(false))
        {
            log("AdvancedServerSwitch_Cancelled".GetLocalized());
            return GameUpdateResult.Cancelled;
        }

        log(string.Format("GameUpdate_PlanSummary".GetLocalized(), ToSizeString(downloadTotalBytes), targetBranch.Tag));

        if (patchAssets.Count > 0)
        {
            log("GameUpdate_StageApplyPatches".GetLocalized());
            await InstallOrPatchAssetsAsync(context, patchAssets).ConfigureAwait(false);
            DeletePatchDeprecatedFiles(context, patchBuild!);
        }
        else
        {
            await UpdateDiffAssetsAsync(context, operations).ConfigureAwait(false);
        }

        await _gameChannelSdkService.EnsureSdkAndDeprecatedFilesAsync(gameDir, currentScheme, log, token,
            downloadMonitor is null ? null : downloadMonitor.AddBytes).ConfigureAwait(false);

        log("GameServer_StageVerify".GetLocalized());
        List<AssetProperty> brokenAssets = await VerifyAssetsAsync(context, targetManifest).ConfigureAwait(false);
        if (brokenAssets.Count > 0)
        {
            log(string.Format("GameServer_RepairFound".GetLocalized(), brokenAssets.Count));
            var repairOperations = brokenAssets
                .Select(asset => GameUpdateAssetOperation.Add(targetInfo.ChunkPrefix, targetInfo.ChunkSuffix, asset))
                .ToList();
            await UpdateDiffAssetsAsync(context, repairOperations).ConfigureAwait(false);
            log("GameServer_RepairDone".GetLocalized());
        }
        else
        {
            log("GameServer_VerifyOk".GetLocalized());
        }

        WriteGameVersion(gameDir, targetBranch.Tag);

        log("GameServer_StageCleanup".GetLocalized());
        CleanupChunks(context);

        return GameUpdateResult.Completed;
    }

    public static PredownloadStatus? TryReadPredownloadStatus(string gameDir)
    {
        try
        {
            string path = Path.Combine(gameDir, ChunksDirectoryName, PredownloadStatusFileName);
            if (!File.Exists(path))
            {
                return null;
            }

            return JsonSerializer.Deserialize<PredownloadStatus>(File.ReadAllText(path));
        }
        catch
        {
            return null;
        }
    }

    public static bool IsPredownloadFinished(string gameDir, out PredownloadStatus? status)
    {
        status = TryReadPredownloadStatus(gameDir);
        if (status is null || !status.Finished)
        {
            return false;
        }

        string chunksDir = Path.Combine(gameDir, ChunksDirectoryName);
        int fileCount = Directory.Exists(chunksDir) ? Directory.GetFiles(chunksDir).Length : 0;
        return fileCount - 1 == status.TotalBlocks;
    }

    #endregion
}