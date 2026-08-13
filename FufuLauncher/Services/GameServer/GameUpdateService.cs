/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using FufuLauncher.Constants;
using FufuLauncher.Helpers;
using FufuLauncher.Helpers.Patch;
using FufuLauncher.Models.GameServer;
using FufuLauncher.Protobuf;
using ZstdSharp;

namespace FufuLauncher.Services.GameServer;

public sealed class GameUpdateService
{
    public const string ChunksDirectoryName = "chunks";
    
    public const string PredownloadStatusFileName = "snap_hutao_predownload_status.json";

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
        SophonBranchPayload preBranch = await _sophonBuildClient.GetBranchPayloadAsync(currentScheme, true, token).ConfigureAwait(false);
        if (string.Equals(preBranch.Tag, localVersion, StringComparison.Ordinal))
        {
            log("GameUpdate_NoPredownloadNeeded".GetLocalized());
            return GameUpdateResult.NothingToDo;
        }

        SophonBranchInfo localInfo = await _sophonBuildClient.GetBranchInfoByTagAsync(currentScheme, localVersion, token).ConfigureAwait(false);
        SophonBranchInfo preInfo = await _sophonBuildClient.GetBranchInfoAsync(currentScheme, true, token).ConfigureAwait(false);

        log("GameServer_StageDecodeManifests".GetLocalized());
        SophonManifestProto localManifest = await _sophonBuildClient.DownloadManifestAsync(localInfo, token).ConfigureAwait(false);
        SophonManifestProto preManifest = await _sophonBuildClient.DownloadManifestAsync(preInfo, token).ConfigureAwait(false);

        log("GameServer_StageDiff".GetLocalized());
        List<GameUpdateAssetOperation> operations = BuildDiffOperations(localManifest, preManifest, preInfo.ChunkPrefix, preInfo.ChunkSuffix);
        List<GameUpdateAssetOperation> downloadOperations = operations
            .Where(operation => operation.Kind != GameUpdateAssetOperationKind.Delete)
            .ToList();
        SophonDecodedPatchBuild? patchBuild = await TryDecodePatchBuildAsync(currentScheme, preBranch, localVersion, token).ConfigureAwait(false);
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
            downloadTotalBytes = downloadOperations.Sum(operation => operation.Chunks.Sum(chunk => chunk.AssetChunk.ChunkSize));
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

        var plan = new GameUpdatePlan(GameUpdateOperationKind.Predownload, preBranch.Tag, downloadTotalBytes, downloadTotalBytes, patchAssets.Count > 0);
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
            await DownloadOperationChunksAsync(context, downloadOperations, "GameServer_StageDownloadChunks".GetLocalized()).ConfigureAwait(false);
        }

        status.Finished = true;
        await WritePredownloadStatusAsync(context, status, token).ConfigureAwait(false);
        log("GameUpdate_PredownloadDone".GetLocalized());

        return GameUpdateResult.Completed;
    }
    
    public async Task<GameUpdateResult> UpdateAsync(string gameDir, GameServerScheme currentScheme, bool usePredownloadAsTarget,
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
        SophonBranchPayload targetBranch = await _sophonBuildClient.GetBranchPayloadAsync(currentScheme, usePredownloadAsTarget, token).ConfigureAwait(false);
        if (string.Equals(targetBranch.Tag, localVersion, StringComparison.Ordinal))
        {
            log("GameUpdate_AlreadyLatest".GetLocalized());
            return GameUpdateResult.NothingToDo;
        }

        SophonBranchInfo localInfo = await _sophonBuildClient.GetBranchInfoByTagAsync(currentScheme, localVersion, token).ConfigureAwait(false);
        SophonBranchInfo targetInfo = await _sophonBuildClient.GetBranchInfoAsync(currentScheme, usePredownloadAsTarget, token).ConfigureAwait(false);

        log("GameServer_StageDecodeManifests".GetLocalized());
        SophonManifestProto localManifest = await _sophonBuildClient.DownloadManifestAsync(localInfo, token).ConfigureAwait(false);
        SophonManifestProto targetManifest = await _sophonBuildClient.DownloadManifestAsync(targetInfo, token).ConfigureAwait(false);

        log("GameServer_StageDiff".GetLocalized());
        List<GameUpdateAssetOperation> operations = BuildDiffOperations(localManifest, targetManifest, targetInfo.ChunkPrefix, targetInfo.ChunkSuffix);
        
        SophonDecodedPatchBuild? patchBuild = await TryDecodePatchBuildAsync(currentScheme, targetBranch, localVersion, token).ConfigureAwait(false);
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

    #region 差异计算
    
    private static List<GameUpdateAssetOperation> BuildDiffOperations(SophonManifestProto localManifest, SophonManifestProto targetManifest,
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

            if ((localAsset.AssetHashMd5 ?? string.Empty).Equals(targetAsset.AssetHashMd5 ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            List<SophonChunk> diffChunks = BuildDiffChunks(localAsset, targetAsset, urlPrefix, urlSuffix);
            operations.Add(GameUpdateAssetOperation.Modify(urlPrefix, urlSuffix, localAsset, targetAsset, diffChunks));
        }

        var targetNames = targetManifest.Assets.Select(asset => asset.AssetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var localAsset in localManifest.Assets)
        {
            if (!targetNames.Contains(localAsset.AssetName))
            {
                operations.Add(GameUpdateAssetOperation.Delete(localAsset));
            }
        }

        return operations;
    }
    
    private static List<SophonChunk> BuildDiffChunks(AssetProperty localAsset, AssetProperty targetAsset, string urlPrefix, string urlSuffix)
    {
        return targetAsset.AssetChunks
            .Where(chunk => localAsset.AssetChunks.FirstOrDefault(candidate =>
                (candidate.ChunkDecompressedHashMd5 ?? string.Empty).Equals(chunk.ChunkDecompressedHashMd5 ?? string.Empty, StringComparison.OrdinalIgnoreCase)) is null)
            .Select(chunk => new SophonChunk(urlPrefix, urlSuffix, chunk))
            .ToList();
    }
    
    private static void InitializeDuplicatedChunkNames(GameUpdateContext context, IEnumerable<GameUpdateAssetOperation> operations)
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

    #endregion

    #region 补丁构建与 LDiff 应用
    
    private async Task<SophonDecodedPatchBuild?> TryDecodePatchBuildAsync(GameServerScheme scheme, SophonBranchPayload payload,
        string localVersion, CancellationToken token)
    {
        if (!payload.DiffTags.Contains(localVersion, StringComparer.Ordinal))
        {
            return null;
        }

        SophonPatchBuildResponse? response;
        try
        {
            response = await _sophonBuildClient.GetPatchBuildAsync(scheme, payload, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }

        if (response is null || response.Manifests.Count == 0)
        {
            return null;
        }

        var manifests = new List<SophonDecodedPatchManifest>();
        long downloadTotalBytes = 0;
        long downloadFileCount = 0;
        long uncompressedTotalBytes = 0;
        long installFileCount = 0;

        foreach (var item in response.Manifests)
        {
            if (!string.Equals(item.MatchingField, "game", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (item.DiffDownload is null || item.ManifestDownload is null || !item.Stats.TryGetValue(localVersion, out var stats))
            {
                return null;
            }

            string manifestUrl = $"{item.ManifestDownload.UrlPrefix}/{item.Manifest.Id}?{item.ManifestDownload.UrlSuffix ?? string.Empty}";
            try
            {
                PatchManifest data = await _sophonBuildClient.DownloadPatchManifestAsync(manifestUrl, item.Manifest.Checksum, token).ConfigureAwait(false);

                downloadTotalBytes += stats.CompressedSize;
                downloadFileCount += stats.ChunkCount;
                uncompressedTotalBytes += stats.UncompressedSize;
                installFileCount += stats.FileCount;
                manifests.Add(new SophonDecodedPatchManifest(localVersion, response.Tag,
                    item.DiffDownload.UrlPrefix, item.DiffDownload.UrlSuffix ?? string.Empty, data));
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // ignored
            }
        }

        if (manifests.Count == 0)
        {
            return null;
        }

        return new SophonDecodedPatchBuild(localVersion, response.Tag, downloadTotalBytes, downloadFileCount,
            uncompressedTotalBytes, installFileCount, manifests);
    }
    
    private static List<SophonPatchAsset> EnumeratePatchAssets(SophonDecodedPatchBuild patchBuild)
    {
        var assets = new List<SophonPatchAsset>();
        foreach (var manifest in patchBuild.Manifests)
        {
            foreach (var fileData in manifest.Data.FileDatas)
            {
                PatchEntry? entry = fileData.PatchesEntries.FirstOrDefault(patchEntry =>
                    string.Equals(patchEntry.Key, manifest.OriginalTag, StringComparison.Ordinal));
                if (entry?.PatchInfo is { } patchInfo)
                {
                    assets.Add(new SophonPatchAsset(manifest.UrlPrefix, manifest.UrlSuffix, fileData, patchInfo));
                }
            }
        }

        return assets;
    }
    
    private async Task PredownloadPatchesAsync(GameUpdateContext context, List<SophonPatchAsset> assets)
    {
        var distinct = assets.GroupBy(asset => asset.PatchInfo.Id, StringComparer.Ordinal).Select(group => group.First()).ToList();
        if (distinct.Count == 0)
        {
            return;
        }

        string stage = "GameServer_StageDownloadChunks".GetLocalized();
        long totalBytes = distinct.Sum(asset => asset.PatchInfo.PatchFileSize);
        context.Progress.Report(GameServerConversionProgress.Reset(stage, distinct.Count, totalBytes,
            distinct.Select(asset => asset.PatchInfo.Id).ToList()));

        long doneBytes = 0;
        for (int i = 0; i < distinct.Count; i++)
        {
            context.Token.ThrowIfCancellationRequested();
            SophonPatchAsset asset = distinct[i];
            await DownloadPatchAsync(context, asset).ConfigureAwait(false);
            doneBytes += asset.PatchInfo.PatchFileSize;
            context.Progress.Report(new GameServerConversionProgress(stage, distinct.Count, i + 1, totalBytes, doneBytes, asset.PatchInfo.Id));
        }
    }
    
    private async Task DownloadPatchAsync(GameUpdateContext context, SophonPatchAsset asset)
    {
        if (context.DownloadedPatches.ContainsKey(asset.PatchInfo.Id))
        {
            return;
        }

        await _chunkDownloader.DownloadBlobAsync(asset.PatchInfo.Id, asset.PatchInfo.PatchFileSize, asset.PatchDownloadUrl,
            context.ChunksDir, context.ChunkLocks, context.Token,
            context.DownloadMonitor is null ? null : context.DownloadMonitor.AddBytes).ConfigureAwait(false);

        context.DownloadedPatches.TryAdd(asset.PatchInfo.Id, 0);
    }
    
    private async Task InstallOrPatchAssetsAsync(GameUpdateContext context, List<SophonPatchAsset> assets)
    {
        if (assets.Count == 0)
        {
            return;
        }

        string stage = "GameUpdate_StageApplyPatches".GetLocalized();
        long totalBytes = assets.Sum(asset => asset.FileData.FileSize);
        context.Progress.Report(GameServerConversionProgress.Reset(stage, assets.Count, totalBytes));

        long doneBytes = 0;
        for (int i = 0; i < assets.Count; i++)
        {
            context.Token.ThrowIfCancellationRequested();
            SophonPatchAsset asset = assets[i];
            bool installed = await InstallOrPatchAssetAsync(context, asset).ConfigureAwait(false);
            if (installed)
            {
                doneBytes += asset.FileData.FileSize;
            }
            else
            {
                context.Log(string.Format("GameUpdate_SkipAsset".GetLocalized(), asset.FileData.FileName));
            }

            context.Progress.Report(new GameServerConversionProgress(
                string.Format("GameUpdate_MergingFile".GetLocalized(), i + 1, assets.Count, asset.FileData.FileName),
                assets.Count, i + 1, totalBytes, doneBytes, null));
        }
    }
    
    private async Task<bool> InstallOrPatchAssetAsync(GameUpdateContext context, SophonPatchAsset asset)
    {
        PatchFileData fileData = asset.FileData;
        PatchInfo patchInfo = asset.PatchInfo;

        string assetPath = context.GetGameFilePath(fileData.FileName);
        if (File.Exists(assetPath)
            && fileData.FileHash.Equals(await HashUtility.Md5FileAsync(assetPath, context.Token).ConfigureAwait(false), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        await DownloadPatchAsync(context, asset).ConfigureAwait(false);
        string patchFilePath = context.GetChunkFilePath(patchInfo.Id);
        if (!File.Exists(patchFilePath))
        {
            return false;
        }

        string? assetDirectory = Path.GetDirectoryName(assetPath);
        if (assetDirectory is null)
        {
            return false;
        }

        Directory.CreateDirectory(assetDirectory);

        try
        {
            using (FileStream patchStream = new(patchFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, ChunkDownloader.BufferSize, FileOptions.RandomAccess))
            {
                if (string.IsNullOrEmpty(patchInfo.OriginalFileName))
                {
                    using (FileStream target = new(assetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        await CopyRangeAsync(patchStream, patchInfo.PatchStartOffset, patchInfo.PatchLength, target, context.Token).ConfigureAwait(false);
                    }

                    return true;
                }

                string oldAssetPath = context.GetGameFilePath(fileData.FileName);
                if (!File.Exists(oldAssetPath))
                {
                    return false;
                }
                
                using (FileStream oldStream = new(oldAssetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (Stream diffView = FufuPatch.CreateSubStream(patchStream, patchInfo.PatchStartOffset, patchInfo.PatchLength))
                using (FileStream target = new(assetPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    return FufuPatch.MergeZstd(oldStream, diffView, target);
                }
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
    
    private static void DeletePatchDeprecatedFiles(GameUpdateContext context, SophonDecodedPatchBuild patchBuild)
    {
        foreach (var manifest in patchBuild.Manifests)
        {
            PatchDeleteFilesEntry? entry = manifest.Data.DeleteFilesEntries.FirstOrDefault(item =>
                string.Equals(item.Key, manifest.OriginalTag, StringComparison.Ordinal));
            if (entry?.DeleteFiles is null)
            {
                continue;
            }

            foreach (var info in entry.DeleteFiles.Infos)
            {
                context.Token.ThrowIfCancellationRequested();
                string path = context.GetGameFilePath(info.Name);
                if (File.Exists(path))
                {
                    TryDeleteFile(path);
                }
            }
        }
    }

    private static async Task CopyRangeAsync(FileStream source, long offset, long length, FileStream target, CancellationToken token)
    {
        byte[] buffer = new byte[ChunkDownloader.BufferSize];
        source.Seek(offset, SeekOrigin.Begin);
        while (length > 0)
        {
            int toRead = (int)Math.Min(length, buffer.Length);
            int read = await source.ReadAsync(buffer.AsMemory(0, toRead), token).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new EndOfStreamException("补丁段数据不完整");
            }

            await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
            length -= read;
        }
    }

    #endregion

    #region 分片下载与组装
    
    private async Task UpdateDiffAssetsAsync(GameUpdateContext context, List<GameUpdateAssetOperation> operations)
    {
        List<GameUpdateAssetOperation> mergeOperations = operations
            .Where(operation => operation.Kind is GameUpdateAssetOperationKind.AddOrRepair or GameUpdateAssetOperationKind.Modify)
            .ToList();
        List<GameUpdateAssetOperation> deleteOperations = operations
            .Where(operation => operation.Kind == GameUpdateAssetOperationKind.Delete)
            .ToList();

        if (mergeOperations.Count > 0)
        {
            InitializeDuplicatedChunkNames(context, mergeOperations);
            Directory.CreateDirectory(context.ChunksDir);

            await DownloadOperationChunksAsync(context, mergeOperations, "GameServer_StageDownloadChunks".GetLocalized()).ConfigureAwait(false);
            await MergeOperationAssetsAsync(context, mergeOperations).ConfigureAwait(false);
        }

        foreach (var operation in deleteOperations)
        {
            context.Token.ThrowIfCancellationRequested();
            string path = context.GetGameFilePath(operation.OldAsset!.AssetName);
            if (File.Exists(path))
            {
                TryDeleteFile(path);
            }
        }
    }
    
    private async Task DownloadOperationChunksAsync(GameUpdateContext context, List<GameUpdateAssetOperation> operations, string stage)
    {
        List<SophonChunk> chunks = operations.SelectMany(operation => operation.Chunks).ToList();
        if (chunks.Count == 0)
        {
            return;
        }

        long totalBytes = chunks.Sum(chunk => chunk.AssetChunk.ChunkSize);
        var pendingNames = chunks.Select(chunk => chunk.AssetChunk.ChunkName).Distinct(StringComparer.Ordinal).ToList();
        context.Progress.Report(GameServerConversionProgress.Reset(stage, chunks.Count, totalBytes, pendingNames));

        long doneChunks = 0;
        long doneBytes = 0;
        Action<long>? onBytesTransferred = context.DownloadMonitor is null ? null : context.DownloadMonitor.AddBytes;

        await Parallel.ForEachAsync(chunks, context.ParallelOptions, async (chunk, token) =>
        {
            await _chunkDownloader.DownloadChunkAsync(chunk, context.ChunksDir, context.ChunkLocks, token, onBytesTransferred).ConfigureAwait(false);
            
            long current = Interlocked.Increment(ref doneChunks);
            long bytes = Interlocked.Add(ref doneBytes, chunk.AssetChunk.ChunkSize);
            context.Progress.Report(new GameServerConversionProgress(stage, chunks.Count, (int)current, totalBytes, bytes, chunk.AssetChunk.ChunkName));
        }).ConfigureAwait(false);
    }
    
    private async Task MergeOperationAssetsAsync(GameUpdateContext context, List<GameUpdateAssetOperation> operations)
    {
        string stage = "GameUpdate_StageMerge".GetLocalized();
        int total = operations.Count;
        context.Progress.Report(GameServerConversionProgress.Reset(stage, total, 0));

        for (int i = 0; i < operations.Count; i++)
        {
            context.Token.ThrowIfCancellationRequested();
            GameUpdateAssetOperation operation = operations[i];
            bool merged = operation.Kind switch
            {
                GameUpdateAssetOperationKind.AddOrRepair => await MergeNewAssetAsync(context, operation.NewAsset!).ConfigureAwait(false),
                GameUpdateAssetOperationKind.Modify => await MergeDiffAssetAsync(context, operation).ConfigureAwait(false),
                _ => true,
            };

            context.Progress.Report(new GameServerConversionProgress(
                string.Format("GameUpdate_MergingFile".GetLocalized(), i + 1, total, operation.NewAsset!.AssetName),
                total, i + 1, 0, 0, null));

            if (!merged)
            {
                context.Log(string.Format("GameUpdate_SkipAsset".GetLocalized(), operation.NewAsset!.AssetName));
            }
        }
    }
    
    private async Task<bool> MergeNewAssetAsync(GameUpdateContext context, AssetProperty asset)
    {
        string targetPath = context.GetGameFilePath(asset.AssetName);
        string? targetDirectory = Path.GetDirectoryName(targetPath);
        if (targetDirectory is null)
        {
            return false;
        }

        Directory.CreateDirectory(targetDirectory);

        int missingChunks = 0;
        using (SafeFileHandle fileHandle = File.OpenHandle(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, preallocationSize: asset.AssetSize))
        {
            await Parallel.ForEachAsync(asset.AssetChunks, context.ParallelOptions, async (chunk, token) =>
            {
                string chunkPath = context.GetChunkFilePath(chunk.ChunkName);
                if (!File.Exists(chunkPath))
                {
                    Interlocked.Increment(ref missingChunks);
                    return;
                }

                using (await context.ChunkLocks.LockAsync(chunk.ChunkName, token).ConfigureAwait(false))
                {
                    byte[] buffer = new byte[ChunkDownloader.BufferSize];
                    long offset = chunk.ChunkOnFileOffset;

                    using (FileStream chunkFile = new(chunkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using var decompressor = new DecompressionStream(chunkFile);
                        while (true)
                        {
                            int read = await decompressor.ReadAsync(buffer, token).ConfigureAwait(false);
                            if (read <= 0)
                            {
                                break;
                            }

                            await RandomAccess.WriteAsync(fileHandle, buffer.AsMemory(0, read), offset, token).ConfigureAwait(false);
                            offset += read;
                        }
                    }
                }

                if (!context.DuplicatedChunkNames.ContainsKey(chunk.ChunkName))
                {
                    TryDeleteFile(chunkPath);
                }
            }).ConfigureAwait(false);
        }

        if (missingChunks > 0)
        {
            TryDeleteFile(targetPath);
            return false;
        }

        return true;
    }
    
    private async Task<bool> MergeDiffAssetAsync(GameUpdateContext context, GameUpdateAssetOperation operation)
    {
        AssetProperty oldAsset = operation.OldAsset!;
        AssetProperty newAsset = operation.NewAsset!;

        string oldAssetPath = context.GetGameFilePath(oldAsset.AssetName);
        if (!File.Exists(oldAssetPath))
        {
            return false;
        }

        string targetPath = context.GetGameFilePath(newAsset.AssetName);
        string? targetDirectory = Path.GetDirectoryName(targetPath);
        if (targetDirectory is null)
        {
            return false;
        }

        Directory.CreateDirectory(targetDirectory);

        byte[] buffer = new byte[ChunkDownloader.BufferSize];
        using (var targetFile = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            targetFile.SetLength(newAsset.AssetSize);

            foreach (var chunk in newAsset.AssetChunks)
            {
                context.Token.ThrowIfCancellationRequested();
                targetFile.Seek(chunk.ChunkOnFileOffset, SeekOrigin.Begin);
                long remaining = chunk.ChunkSizeDecompressed;

                var oldChunk = oldAsset.AssetChunks.FirstOrDefault(candidate =>
                    (candidate.ChunkDecompressedHashMd5 ?? string.Empty).Equals(chunk.ChunkDecompressedHashMd5 ?? string.Empty, StringComparison.OrdinalIgnoreCase));

                if (oldChunk is not null)
                {
                    using (var localFile = new FileStream(oldAssetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        localFile.Seek(oldChunk.ChunkOnFileOffset, SeekOrigin.Begin);
                        while (remaining > 0)
                        {
                            int read = await localFile.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), context.Token).ConfigureAwait(false);
                            if (read <= 0)
                            {
                                break;
                            }

                            await targetFile.WriteAsync(buffer.AsMemory(0, read), context.Token).ConfigureAwait(false);
                            remaining -= read;
                        }
                    }
                }
                else
                {
                    string chunkPath = context.GetChunkFilePath(chunk.ChunkName);
                    if (!File.Exists(chunkPath))
                    {
                        TryDeleteFile(targetPath);
                        return false;
                    }

                    using (await context.ChunkLocks.LockAsync(chunk.ChunkName, context.Token).ConfigureAwait(false))
                    {
                        using (FileStream chunkFile = new(chunkPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            using var decompressor = new DecompressionStream(chunkFile);
                            while (remaining > 0)
                            {
                                int read = await decompressor.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), context.Token).ConfigureAwait(false);
                                if (read <= 0)
                                {
                                    break;
                                }

                                await targetFile.WriteAsync(buffer.AsMemory(0, read), context.Token).ConfigureAwait(false);
                                remaining -= read;
                            }
                        }
                    }

                    if (!context.DuplicatedChunkNames.ContainsKey(chunk.ChunkName))
                    {
                        TryDeleteFile(chunkPath);
                    }
                }
            }
        }

        return true;
    }

    #endregion

    #region 校验修复
    
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
                    using SafeFileHandle fileHandle = File.OpenHandle(assetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, FileOptions.RandomAccess);

                    foreach (var chunk in asset.AssetChunks)
                    {
                        token.ThrowIfCancellationRequested();

                        byte[] buffer = new byte[checked((int)chunk.ChunkSizeDecompressed)];
                        await ReadExactlyAsync(fileHandle, buffer, chunk.ChunkOnFileOffset, token).ConfigureAwait(false);

                        string actualMd5 = HashUtility.Md5Bytes(buffer);
                        if (!actualMd5.Equals(chunk.ChunkDecompressedHashMd5 ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                        {
                            isBroken = true;
                            break;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException or EndOfStreamException or ArgumentException or OverflowException or UnauthorizedAccessException)
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
    
    private static async Task ReadExactlyAsync(SafeFileHandle handle, Memory<byte> buffer, long offset, CancellationToken token)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = await RandomAccess.ReadAsync(handle, buffer[total..], offset + total, token).ConfigureAwait(false);
            if (read <= 0)
            {
                throw new EndOfStreamException("文件末尾早于预期");
            }

            total += read;
        }
    }

    #endregion

    #region 辅助

    private static void EnsureDirectoryPermissions(string gameDir)
    {
        try
        {
            Directory.CreateDirectory(gameDir);
            string tempFilePath = Path.Combine(gameDir, $"{Guid.NewGuid():N}.tmp");
            string movedFilePath = Path.Combine(gameDir, $"{Guid.NewGuid():N}.tmp");

            using (SafeFileHandle handle = File.OpenHandle(tempFilePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, preallocationSize: 32 * 1024))
            {
                RandomAccess.Write(handle, "FUFU LAUNCHER DIRECTORY PERMISSION CHECK"u8, 0);
                RandomAccess.FlushToDisk(handle);
            }

            File.Move(tempFilePath, movedFilePath);
            File.Delete(movedFilePath);
        }
        catch (Exception ex)
        {
            throw new UnauthorizedAccessException(string.Format("GameServer_InsufficientPermission".GetLocalized(), ex.Message), ex);
        }
    }
    
    private static void EnsureFreeSpace(GameUpdateContext context, long installTotalBytes)
    {
        long existingChunks = GetExistingChunksSize(context);
        long required = installTotalBytes - existingChunks + TempFileMarginBytes;
        if (required <= 0)
        {
            return;
        }

        string? root = Path.GetPathRoot(context.GameDir);
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        try
        {
            var drive = new DriveInfo(root);
            if (drive.AvailableFreeSpace < required)
            {
                throw new InvalidOperationException(string.Format("GameUpdate_NoFreeSpace".GetLocalized(),
                    ToSizeString(required), ToSizeString(drive.AvailableFreeSpace)));
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch
        {
            // ignored
        }
    }
    
    private static long GetExistingChunksSize(GameUpdateContext context)
    {
        if (!Directory.Exists(context.ChunksDir))
        {
            return 0;
        }

        long size = 0;
        DateTime cutoffDate = DateTime.Now.AddDays(-5);

        try
        {
            foreach (string file in Directory.EnumerateFiles(context.ChunksDir, "*.*", SearchOption.AllDirectories))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime <= cutoffDate)
                    {
                        TryDeleteFile(file);
                        continue;
                    }

                    size += fileInfo.Length;
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
        catch
        {
            return 0;
        }

        return size;
    }
    
    private static void WriteGameVersion(string gameDir, string tag)
    {
        string configPath = Path.Combine(gameDir, GameConstants.CONFIG_FILE_NAME);
        if (!File.Exists(configPath))
        {
            return;
        }

        var ini = new IniFile(configPath);
        ini.UpdateMultiple(new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["General"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["game_version"] = tag,
            },
        });
    }

    private static async Task WritePredownloadStatusAsync(GameUpdateContext context, PredownloadStatus status, CancellationToken token)
    {
        using FileStream stream = File.Create(context.PredownloadStatusFilePath);
        await JsonSerializer.SerializeAsync(stream, status, cancellationToken: token).ConfigureAwait(false);
    }

    private static void CleanupChunks(GameUpdateContext context)
    {
        try
        {
            if (Directory.Exists(context.ChunksDir))
            {
                Directory.Delete(context.ChunksDir, true);
            }
        }
        catch (Exception ex)
        {
            context.Log(string.Format("GameServer_ChunkCleanupFailed".GetLocalized(), ex.Message));
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }

    private static string ToSizeString(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
        {
            return $"{bytes / 1073741824.0:F1} GB";
        }

        if (bytes >= 1024L * 1024L)
        {
            return $"{bytes / 1048576.0:F1} MB";
        }

        return $"{bytes / 1024.0:F1} KB";
    }

    #endregion
}
