/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Models;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisModel
{
    #region UID & Metadata Commands

    [RelayCommand]
    private async Task SwitchUidAsync(string uid)
    {
        if (string.IsNullOrEmpty(uid) || uid == _currentUid) return;
        await SwitchToUidAsync(uid);
    }

    [RelayCommand]
    private async Task AddNewUserAsync()
    {
        if (!string.IsNullOrEmpty(_currentUid))
        {
            SaveGachaLogsToDb();
            _uidBeforeAddNew = _currentUid;
        }

        _currentUid = "";
        _cachedCharacterLogs.Clear();
        _cachedWeaponLogs.Clear();
        _cachedChronicledLogs.Clear();
        _cachedNoviceLogs.Clear();
        _cachedStandardLogs.Clear();

        App.MainWindow.DispatcherQueue.TryEnqueue(() =>
        {
            SelectedUid = "";
            ClearCollections();
            CharacterStats = new GachaStatistic { PoolName = "角色活动" };
            WeaponStats = new GachaStatistic { PoolName = "武器活动" };
            ChronicledStats = new GachaStatistic { PoolName = "集录祈愿" };
            StandardStats = new GachaStatistic { PoolName = "常驻祈愿" };
            InvalidateAnalysisDashboard();
            HasGachaData = false;
            CrawlerStatus = "等待获取数据...";
        });
    }

    [RelayCommand]
    private void PreFetchMetadata()
    {
        if (IsScraping) return;
        IsScraping = true;
        CrawlerStatus = "正在刷新角色、武器与卡池元数据...";
        RequestMetadataScrapeAction?.Invoke();
    }

    #endregion
}
