/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Data.Entities;
using FufuLauncher.Messages;
using FufuLauncher.Models;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisModel
{
    #region Persistence & UID Management

    private void InitializeDatabase()
    {
        // EF Core EnsureCreated() handles table creation in the repository.
        // Legacy ad-hoc migrations (Rank/ItemId columns, PK change) are no longer
        // needed since EF Core creates tables with the correct schema from the start.
        // For existing databases, EF Core will see tables exist and skip creation.
    }

    private void LoadMetadataFromDb()
    {
        _savedMetadata.Clear();
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            CharacterMetadataPreview.Clear();
            WeaponMetadataPreview.Clear();
        });

        var entities = _metadataRepo.GetAllMetadata();
        foreach (var entity in entities)
        {
            var item = new ScrapedMetadata
            {
                Name = entity.Name,
                ImgSrc = string.IsNullOrWhiteSpace(entity.ImgSrc) ? null : entity.ImgSrc,
                ElementSrc = string.IsNullOrWhiteSpace(entity.ElementSrc) ? null : entity.ElementSrc,
                Type = entity.Type,
                Rank = entity.Rank,
                ItemId = entity.ItemId
            };
            _savedMetadata.Add(item);
            var isChar = item.Type == "char";
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                if (isChar) CharacterMetadataPreview.Add(item);
                else WeaponMetadataPreview.Add(item);
            });
        }
    }

    private void SaveMetadataToDb(List<ScrapedMetadata> newItems)
    {
        var entities = newItems.Select(item => new MetadataEntity
        {
            Name = item.Name ?? "",
            ImgSrc = item.ImgSrc ?? "",
            ElementSrc = item.ElementSrc ?? "",
            Type = item.Type ?? "",
            Rank = item.Rank ?? "",
            ItemId = item.ItemId ?? ""
        }).ToList();
        _metadataRepo.UpsertMetadata(entities);
    }

    private List<string> QueryKnownUidsFromDb()
    {
        return _metadataRepo.GetDistinctUids();
    }

    private void RefreshKnownUids()
    {
        var uids = QueryKnownUidsFromDb();
        var current = _currentUid;
        Debug.WriteLine($"[Gacha] RefreshKnownUids: 查询到 {uids.Count} 个 UID: [{string.Join(", ", uids)}], current={current}");
        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            RefreshKnownUidsUI(uids);
            if (!string.IsNullOrEmpty(current) && UidComboItems.Contains(current))
                SelectedUid = current;
        });
    }

    private void RefreshKnownUidsUI(List<string> uids)
    {
        KnownUids.Clear();
        UidComboItems.Clear();
        foreach (var uid in uids)
        {
            KnownUids.Add(uid);
            UidComboItems.Add(uid);
        }
        UidComboItems.Add(AddNewUserItem);
    }

    private void LoadGachaLogsFromDb(string uid)
    {
        _cachedCharacterLogs.Clear();
        _cachedWeaponLogs.Clear();
        _cachedChronicledLogs.Clear();
        _cachedNoviceLogs.Clear();
        _cachedStandardLogs.Clear();

        if (string.IsNullOrEmpty(uid)) return;

        try
        {
            var entities = _metadataRepo.GetGachaLogsByUid(uid);
            foreach (var entity in entities)
            {
                var item = new GachaLogItem
                {
                    Id = entity.Id,
                    Uid = uid,
                    GachaType = entity.GachaType,
                    ItemId = entity.ItemId,
                    Count = entity.Count,
                    Time = entity.Time,
                    Name = entity.Name,
                    Lang = entity.Lang,
                    ItemType = entity.ItemType,
                    RankType = entity.RankType
                };
                var gt = GetNormalizedGachaType(item.GachaType);
                if (gt == "301") _cachedCharacterLogs.Add(item);
                else if (gt == "302") _cachedWeaponLogs.Add(item);
                else if (gt == "500") _cachedChronicledLogs.Add(item);
                else if (gt == "100") _cachedNoviceLogs.Add(item);
                else _cachedStandardLogs.Add(item);
            }
            Debug.WriteLine($"[Gacha] 加载完成 UID={uid}: 角色{_cachedCharacterLogs.Count} 武器{_cachedWeaponLogs.Count} 集录{_cachedChronicledLogs.Count} 新手{_cachedNoviceLogs.Count} 常驻{_cachedStandardLogs.Count}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gacha] 加载抽卡数据失败: {ex.Message}");
        }
    }

    private void SaveGachaLogsToDb()
    {
        if (string.IsNullOrEmpty(_currentUid)) { Debug.WriteLine("[Gacha] SaveGachaLogsToDb: _currentUid 为空，跳过保存"); return; }
        try
        {
            var totalBefore = _cachedCharacterLogs.Count + _cachedWeaponLogs.Count + _cachedChronicledLogs.Count + _cachedNoviceLogs.Count + _cachedStandardLogs.Count;
            Debug.WriteLine($"[Gacha] SaveGachaLogsToDb: 开始保存 UID={_currentUid}, 共 {totalBefore} 条记录");

            var allLogs = new List<GachaLogEntity>();
            AddLogItems(allLogs, _cachedCharacterLogs);
            AddLogItems(allLogs, _cachedWeaponLogs);
            AddLogItems(allLogs, _cachedChronicledLogs);
            AddLogItems(allLogs, _cachedNoviceLogs);
            AddLogItems(allLogs, _cachedStandardLogs);

            _metadataRepo.ReplaceGachaLogs(_currentUid, allLogs);
            Debug.WriteLine($"[Gacha] 保存完成 UID={_currentUid}: 角色{_cachedCharacterLogs.Count} 武器{_cachedWeaponLogs.Count} 集录{_cachedChronicledLogs.Count} 新手{_cachedNoviceLogs.Count} 常驻{_cachedStandardLogs.Count}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gacha] 保存抽卡数据失败: {ex.Message}");
        }
    }

    private static void AddLogItems(List<GachaLogEntity> target, List<GachaLogItem> source)
    {
        foreach (var item in source)
        {
            target.Add(new GachaLogEntity
            {
                Id = item.Id ?? "",
                GachaType = item.GachaType ?? "",
                ItemId = item.ItemId,
                Count = item.Count,
                Time = item.Time,
                Name = item.Name,
                Lang = item.Lang,
                ItemType = item.ItemType,
                RankType = item.RankType
            });
        }
    }

    private async Task SwitchToUidAsync(string uid)
    {
        if (_currentUid == uid) return;
        if (!string.IsNullOrEmpty(_currentUid))
            SaveGachaLogsToDb();

        _currentUid = uid;
        LoadGachaLogsFromDb(uid);

        _ = _localSettingsService.SaveSettingAsync(LastSelectedUidKey, uid);

        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            SelectedUid = uid;
            if (_cachedCharacterLogs.Count + _cachedWeaponLogs.Count + _cachedChronicledLogs.Count + _cachedStandardLogs.Count > 0)
            {
                RefreshUIFromCache();
                HasGachaData = true;
                CrawlerStatus = $"已切换到 UID: {uid}";
            }
            else
            {
                ClearCollections();
                CharacterStats = new GachaStatistic { PoolName = "角色活动" };
                WeaponStats = new GachaStatistic { PoolName = "武器活动" };
                ChronicledStats = new GachaStatistic { PoolName = "集录祈愿" };
                StandardStats = new GachaStatistic { PoolName = "常驻祈愿" };
                InvalidateAnalysisDashboard();
                HasGachaData = false;
                CrawlerStatus = "该账号暂无抽卡记录";
            }
        });
    }

    private async Task<bool> HandleUidMismatchAsync(string incomingUid)
    {
        if (string.IsNullOrEmpty(incomingUid)) return true;
        if (string.IsNullOrEmpty(_currentUid)) return true;
        if (_currentUid == incomingUid) return true;

        if (OnUidMismatchAsync != null)
        {
            var accepted = await OnUidMismatchAsync(_currentUid, incomingUid);
            if (accepted)
            {
                await SwitchToUidAsync(incomingUid);
                return true;
            }
            return false;
        }
        return false;
    }

    public async Task ClearGachaDataAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_currentUid))
            {
                WeakReferenceMessenger.Default.Send(new NotificationMessage("删除失败", "当前没有选中任何账号", NotificationType.Error, 3000));
                return;
            }

            var deletedUid = _currentUid;

            _metadataRepo.DeleteGachaLogsByUid(deletedUid);

            var remainingUids = QueryKnownUidsFromDb();

            if (remainingUids.Count > 0)
            {
                var switchToUid = remainingUids[0];
                _currentUid = switchToUid;
                LoadGachaLogsFromDb(switchToUid);

                _ = _localSettingsService.SaveSettingAsync(LastSelectedUidKey, switchToUid);

                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    RefreshKnownUidsUI(remainingUids);
                    SelectedUid = switchToUid;
                    RefreshUIFromCache();
                    HasGachaData = true;
                    CrawlerStatus = $"已删除 UID: {deletedUid} 的记录，已切换到 UID: {switchToUid}";
                });
            }
            else
            {
                _currentUid = "";
                _cachedCharacterLogs.Clear();
                _cachedWeaponLogs.Clear();
                _cachedChronicledLogs.Clear();
                _cachedNoviceLogs.Clear();
                _cachedStandardLogs.Clear();

                _ = _localSettingsService.SaveSettingAsync(LastSelectedUidKey, "");

                App.MainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    RefreshKnownUidsUI(remainingUids);
                    ClearCollections();
                    CharacterStats = new GachaStatistic { PoolName = "角色活动" };
                    WeaponStats = new GachaStatistic { PoolName = "武器活动" };
                    ChronicledStats = new GachaStatistic { PoolName = "集录祈愿" };
                    StandardStats = new GachaStatistic { PoolName = "常驻祈愿" };
                    InvalidateAnalysisDashboard();
                    GachaUrl = string.Empty;
                    HasGachaData = false;
                    SelectedUid = "";
                    CrawlerStatus = "数据已清空";
                });
            }

            WeakReferenceMessenger.Default.Send(new NotificationMessage("删除成功", $"已删除 UID: {deletedUid} 的抽卡记录", NotificationType.Success, 3000));
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage("删除失败", $"详细信息: {ex.Message}", NotificationType.Error, 5000));
        }
    }

    #endregion
}
