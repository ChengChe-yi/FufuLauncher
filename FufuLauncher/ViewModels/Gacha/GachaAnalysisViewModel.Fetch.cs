/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisViewModel
{
    #region Gacha Record Fetching

    [RelayCommand]
    private async Task FetchGachaDataAsync()
    {
        if (IsFetching || IsScraping) return;
        if (string.IsNullOrWhiteSpace(GachaUrl))
        {
            CrawlerStatus = "Gacha_PleaseEnterValidUrl".GetLocalized();
            return;
        }

        IsFetching = true;
        CrawlerStatus = "Gacha_ParsingApiUrl".GetLocalized();

        try
        {
            var link = GachaUrlHelper.Parse(GachaUrl);
            if (link == null)
            {
                CrawlerStatus = "Gacha_InvalidUrlFormat".GetLocalized();
                IsFetching = false;
                return;
            }

            await FetchFromLinkAsync(link);
        }
        catch (Exception ex)
        {
            CrawlerStatus = string.Format("Common_OperationFailed_Format".GetLocalized(), ex.Message);
            IsFetching = false;
            OnErrorAction?.Invoke(CrawlerStatus);
        }

        if (!IsScraping) IsFetching = false;
        await FinishPendingRoleSwitchAsync();
    }

    [RelayCommand]
    private async Task FetchFromGameCacheAsync()
    {
        if (IsFetching || IsScraping) return;
        IsFetching = true;
        CrawlerStatus = "GachaAnalysis_CacheReading".GetLocalized();
        try
        {
            var gamePath = App.GetService<IGameLauncherService>().GetGamePath();
            if (string.IsNullOrWhiteSpace(gamePath))
            {
                CacheFetchFailed("GachaAnalysis_CacheMissingPath");
                return;
            }

            var link = await GachaCacheService.FindLatestLinkAsync(gamePath);
            if (link == null)
            {
                CacheFetchFailed("GachaAnalysis_CacheNoLink");
                return;
            }

            await FetchFromLinkAsync(link);
        }
        catch (DirectoryNotFoundException)
        {
            CacheFetchFailed("GachaAnalysis_CacheMissingPath");
        }
        catch (FileNotFoundException)
        {
            CacheFetchFailed("GachaAnalysis_CacheNoLink");
        }
        catch (IOException)
        {
            CacheFetchFailed("GachaAnalysis_CacheReadFailed");
        }
        catch (Exception ex)
        {
            CrawlerStatus = string.Format("Common_OperationFailed_Format".GetLocalized(), ex.Message);
            OnErrorAction?.Invoke(CrawlerStatus);
        }
        finally
        {
            if (!IsScraping) IsFetching = false;
            await FinishPendingRoleSwitchAsync();
        }
    }

    private void CacheFetchFailed(string resourceKey)
    {
        CrawlerStatus = resourceKey.GetLocalized();
        OnErrorAction?.Invoke(CrawlerStatus);
    }

    private async Task FetchFromLinkAsync(GachaLink link, string? expectedUid = null, bool incremental = false,
        bool switchToSelectedRole = false)
    {
        var roleVersion = _roleVersion;
        bool CanApply() => roleVersion == _roleVersion && !_pendingRoleSwitch;
        var useBaseline = incremental && !string.IsNullOrEmpty(expectedUid) && _currentUid == expectedUid;

        void OnProgress(string pool, int count) =>
            App.MainWindow.DispatcherQueue.TryEnqueue(() => CrawlerStatus = string.Format("Gacha_FetchPoolRecordsProgress_Format".GetLocalized(), pool, count));

        var charPoolName = "Gacha_Pool_Character".GetLocalized();
        var weaponPoolName = "Gacha_Pool_Weapon".GetLocalized();
        var chronicledPoolName = "Gacha_Pool_Chronicled".GetLocalized();
        var novicePoolName = "Gacha_Pool_Novice".GetLocalized();
        var standardPoolName = "Gacha_Pool_Standard".GetLocalized();

        CrawlerStatus = string.Format("Gacha_FetchingType_Format".GetLocalized(), charPoolName);
        var charLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "301", count => OnProgress(charPoolName, count),
            useBaseline ? GetNewestLogId(_cachedCharacterLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);
        CrawlerStatus = string.Format("Gacha_PoolCountNextPool_Format".GetLocalized(), charPoolName, charLogs.Count, weaponPoolName);
        var weaponLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "302", count => OnProgress(weaponPoolName, count),
            useBaseline ? GetNewestLogId(_cachedWeaponLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);
        CrawlerStatus = string.Format("Gacha_PoolCountNextPool_Format".GetLocalized(), weaponPoolName, weaponLogs.Count, chronicledPoolName);
        var chronicledLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "500",
            count => OnProgress(chronicledPoolName, count),
            useBaseline ? GetNewestLogId(_cachedChronicledLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);
        CrawlerStatus = string.Format("Gacha_PoolCountNextPool_Format".GetLocalized(), chronicledPoolName, chronicledLogs.Count, novicePoolName);
        var noviceLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "100", count => OnProgress(novicePoolName, count),
            useBaseline ? GetNewestLogId(_cachedNoviceLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);
        CrawlerStatus = string.Format("Gacha_PoolCountNextPool_Format".GetLocalized(), novicePoolName, noviceLogs.Count, standardPoolName);
        var standardLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "200",
            count => OnProgress(standardPoolName, count),
            useBaseline ? GetNewestLogId(_cachedStandardLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);


        var allFetched = charLogs.Concat(weaponLogs).Concat(chronicledLogs).Concat(noviceLogs).Concat(standardLogs)
            .ToList();
        if (!CanApply())
        {
            CrawlerStatus = "Gacha_RoleSwitchedDiscardOld".GetLocalized();
            return;
        }

        if (allFetched.Count == 0)
        {
            if (!string.IsNullOrEmpty(expectedUid) && _currentUid == expectedUid &&
                _cachedCharacterLogs.Count + _cachedWeaponLogs.Count + _cachedChronicledLogs.Count +
                _cachedNoviceLogs.Count + _cachedStandardLogs.Count > 0)
            {
                CrawlerStatus = string.Format("Gacha_NoNewRecordsKeptExisting_Format".GetLocalized(), expectedUid);
                return;
            }

            CrawlerStatus = "Gacha_NoRecordsKeptExisting".GetLocalized();
            OnErrorAction?.Invoke(CrawlerStatus);
            return;
        }

        var uids = allFetched.Select(l => l.Uid).Distinct().ToList();
        if (uids.Count != 1)
            throw new GachaFetchException("Gacha_MultiAccountMismatchError".GetLocalized(), null);
        var fetchedUid = uids[0];
        if (switchToSelectedRole)
        {
            if (string.IsNullOrEmpty(expectedUid) || fetchedUid != expectedUid)
                throw new GachaFetchException("Gacha_RoleUidMismatchError".GetLocalized(), null);

            await SwitchToUidAsync(fetchedUid);
        }
        else if (!await HandleUidMismatchAsync(fetchedUid))
        {
            CrawlerStatus = "Gacha_ImportCancelledExistingUnchanged".GetLocalized();
            return;
        }

        if (!CanApply()) return;
        if (!switchToSelectedRole) _archiveSelectionOverride = true;
        _currentUid = fetchedUid;
        _uidBeforeAddNew = "";
        _cachedCharacterLogs = MergeLogs(_cachedCharacterLogs, charLogs);
        _cachedWeaponLogs = MergeLogs(_cachedWeaponLogs, weaponLogs);
        _cachedChronicledLogs = MergeLogs(_cachedChronicledLogs, chronicledLogs);
        _cachedNoviceLogs = MergeLogs(_cachedNoviceLogs, noviceLogs);
        _cachedStandardLogs = MergeLogs(_cachedStandardLogs, standardLogs);
        FillMissingFieldsFromMetadata(charLogs, weaponLogs, chronicledLogs, noviceLogs, standardLogs);
        CrawlerStatus = string.Format("Gacha_FetchDoneCheckingImages_Format".GetLocalized(), fetchedUid, allFetched.Count);
        HasGachaData = true;
        SaveGachaDataAsync();
        if (RequestMetadataScrapeAction != null)
        {
            IsScraping = true;
            RequestMetadataScrapeAction.Invoke();
        }
        else
            RefreshUIFromCache();
    }

    #endregion
}
