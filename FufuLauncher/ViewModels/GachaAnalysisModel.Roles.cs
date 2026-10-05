/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisModel
{
    private GameRoleService RoleService => App.GetService<GameRoleService>();
    public GameRoleScope RoleSelection { get; } = new();
    private bool _archiveSelectionOverride;
    public bool IsUpdatingRoleList { get; private set; }
    private bool _pendingRoleSwitch;
    private int _roleVersion;

    public void SubscribeToRoleChanges()
    {
        WeakReferenceMessenger.Default.Register<GameRoleChangedMessage>(this, (r, m) =>
        {
            if (!_archiveSelectionOverride && !RoleSelection.HasOverride) RoleChanged();
        });
        WeakReferenceMessenger.Default.Register<FeatureGameRoleChangedMessage>(this, (r, m) =>
        {
            if (ReferenceEquals(m.Scope, RoleSelection)) RoleChanged();
        });
        WeakReferenceMessenger.Default.Register<AccountChangedMessage>(this, (r, m) =>
        {
            RoleSelection.Reset();
            _archiveSelectionOverride = false;
            RoleChanged();
        });
        WeakReferenceMessenger.Default.Register<GameRolesUpdatedMessage>(this, (r, m) =>
            App.MainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                RefreshKnownUidsUI(QueryKnownUidsFromDb());
                SelectedUid = _currentUid;
            }));
    }

    public void CleanupRoleChanges() => WeakReferenceMessenger.Default.UnregisterAll(this);

    private void RoleChanged()
    {
        ++_roleVersion;
        _pendingRoleSwitch = true;
        App.MainWindow.DispatcherQueue.TryEnqueue(async () =>
        {
            if (IsDataLoaded && !IsFetching && !IsScraping) await FinishPendingRoleSwitchAsync();
        });
    }

    public async Task InitializeSharedRolesAsync()
    {
        try { await RoleService.RefreshAsync(); }
        catch (Exception ex) { CrawlerStatus = ex.Message; }
        if (!_archiveSelectionOverride)
        {
            _pendingRoleSwitch = true;
            await FinishPendingRoleSwitchAsync();
        }
    }

    private async Task FinishPendingRoleSwitchAsync()
    {
        if (!_pendingRoleSwitch || !IsDataLoaded || IsFetching || IsScraping) return;
        _pendingRoleSwitch = false;
        RefreshKnownUidsUI(QueryKnownUidsFromDb());
        var account = _accountManager.GetActiveAccountEntry();
        if (account == null) return;
        var role = RoleSelection.Current(account);
        await SwitchToUidAsync(role?.game_uid ?? "");
        if (role == null) CrawlerStatus = "GameRole_Unavailable".GetLocalized();
    }
}
