/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisModel
{
    #region Gacha Record Fetching

    [RelayCommand]
    private async Task FetchGachaDataAsync()
    {
        if (IsFetching || IsScraping) return;
        if (string.IsNullOrWhiteSpace(GachaUrl))
        {
            CrawlerStatus = "请输入有效的抽卡链接";
            return;
        }

        IsFetching = true;
        CrawlerStatus = "正在解析 API 链接...";

        try
        {
            var link = GachaUrlHelper.Parse(GachaUrl);
            if (link == null)
            {
                CrawlerStatus = "链接格式错误，无法提取 API 地址";
                IsFetching = false;
                return;
            }

            await FetchFromLinkAsync(link);
        }
        catch (Exception ex)
        {
            CrawlerStatus = $"更新失败: {ex.Message}";
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
        catch (DirectoryNotFoundException) { CacheFetchFailed("GachaAnalysis_CacheMissingPath"); }
        catch (FileNotFoundException) { CacheFetchFailed("GachaAnalysis_CacheNoLink"); }
        catch (IOException) { CacheFetchFailed("GachaAnalysis_CacheReadFailed"); }
        catch (Exception ex)
        {
            CrawlerStatus = $"获取失败: {ex.Message}";
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
            App.MainWindow.DispatcherQueue.TryEnqueue(() => CrawlerStatus = $"正在获取{pool}记录... (已获取 {count} 条)");

        CrawlerStatus = "正在获取角色活动记录...";
        var charLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "301", count => OnProgress("角色活动", count),
            useBaseline ? GetNewestLogId(_cachedCharacterLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);
        CrawlerStatus = $"角色活动 {charLogs.Count} 条，正在获取武器活动记录...";
        var weaponLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "302", count => OnProgress("武器活动", count),
            useBaseline ? GetNewestLogId(_cachedWeaponLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);
        CrawlerStatus = $"武器活动 {weaponLogs.Count} 条，正在获取集录祈愿记录...";
        var chronicledLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "500", count => OnProgress("集录祈愿", count),
            useBaseline ? GetNewestLogId(_cachedChronicledLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);
        CrawlerStatus = $"集录祈愿 {chronicledLogs.Count} 条，正在获取新手祈愿记录...";
        var noviceLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "100", count => OnProgress("新手祈愿", count),
            useBaseline ? GetNewestLogId(_cachedNoviceLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);
        CrawlerStatus = $"新手祈愿 {noviceLogs.Count} 条，正在获取常驻祈愿记录...";
        var standardLogs = await _gachaService.FetchGachaLogAsync(link.ApiUrl, "200", count => OnProgress("常驻祈愿", count),
            useBaseline ? GetNewestLogId(_cachedStandardLogs) : 0,
            requireComplete: true, expectedRegion: link.Region, expectedUid: expectedUid);

        // Stage all pools before switching archives or modifying existing records.
        var allFetched = charLogs.Concat(weaponLogs).Concat(chronicledLogs).Concat(noviceLogs).Concat(standardLogs).ToList();
        if (!CanApply())
        {
            CrawlerStatus = "角色已切换，已丢弃旧角色的获取结果。";
            return;
        }
        if (allFetched.Count == 0)
        {
            if (!string.IsNullOrEmpty(expectedUid) && _currentUid == expectedUid &&
                _cachedCharacterLogs.Count + _cachedWeaponLogs.Count + _cachedChronicledLogs.Count +
                _cachedNoviceLogs.Count + _cachedStandardLogs.Count > 0)
            {
                CrawlerStatus = $"UID {expectedUid} 未获取到新记录，已保留现有数据。";
                return;
            }
            CrawlerStatus = "未获取到祈愿记录，已保留现有数据。请确认游戏内祈愿历史中有可查询的记录。";
            OnErrorAction?.Invoke(CrawlerStatus);
            return;
        }
        var uids = allFetched.Select(l => l.Uid).Distinct().ToList();
        if (uids.Count != 1)
            throw new GachaFetchException("不同卡池返回了不同账号的数据，已停止导入。", null);
        var fetchedUid = uids[0];
        if (switchToSelectedRole)
        {
            if (string.IsNullOrEmpty(expectedUid) || fetchedUid != expectedUid)
                throw new GachaFetchException("返回记录的 UID 与所选游戏角色不一致，已停止导入。", null);
            // The user already chose this role. Switch only after every pool has been validated.
            await SwitchToUidAsync(fetchedUid);
        }
        else if (!await HandleUidMismatchAsync(fetchedUid))
        {
            CrawlerStatus = "已取消导入，现有数据未更改。";
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
        CrawlerStatus = $"获取完成，UID {fetchedUid}，共 {allFetched.Count} 条记录，正在检查图片资源...";
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
