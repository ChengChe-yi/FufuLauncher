/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisModel
{
    #region UID & Metadata Commands

    [RelayCommand]
    private async Task SwitchUidAsync(string uid)
    {
        if (IsFetching || IsScraping || string.IsNullOrEmpty(uid) || uid == _currentUid) return;
        var account = _accountManager.GetActiveAccountEntry();
        var role = account?.GameRoles?.FirstOrDefault(r => r.game_uid == uid);
        _archiveSelectionOverride = true;
        if (role != null) await RoleService.SelectAsync(account!.Id, role, RoleSelection);
        await SwitchToUidAsync(uid);
    }

    [RelayCommand]
    private async Task AddNewUserAsync()
    {
        _archiveSelectionOverride = true;
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
            CharacterStats = new GachaStatistic { PoolName = "Gacha_Pool_Character".GetLocalized() };
            WeaponStats = new GachaStatistic { PoolName = "Gacha_Pool_Weapon".GetLocalized() };
            ChronicledStats = new GachaStatistic { PoolName = "Gacha_Pool_Chronicled".GetLocalized() };
            StandardStats = new GachaStatistic { PoolName = "Gacha_Pool_Standard".GetLocalized() };
            InvalidateAnalysisDashboard();
            HasGachaData = false;
            CrawlerStatus = "Gacha_WaitingForData".GetLocalized();
        });
    }

    [RelayCommand]
    private void PreFetchMetadata()
    {
        if (IsScraping) return;
        IsScraping = true;
        CrawlerStatus = "Gacha_RefreshingMetadata".GetLocalized();
        RequestMetadataScrapeAction?.Invoke();
    }

    #endregion
}