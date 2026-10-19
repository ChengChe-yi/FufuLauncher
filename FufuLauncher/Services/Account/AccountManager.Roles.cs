/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Contracts.Services;

namespace FufuLauncher.Services;

public partial class AccountManager
{
    public async Task UpdateGameRolesAsync(string accountId, IEnumerable<GameRoleInfo> roles)
    {
        await _lock.WaitAsync();
        try
        {
            var entry = _accountList.Accounts.FirstOrDefault(a => a.Id == accountId)
                        ?? throw new InvalidOperationException("社区账号已移除。");
            var oldRoles = entry.GameRoles;
            var oldUid = entry.GameUid;
            var oldRegion = entry.GameRegion;
            GameRoleSelection.UpdateBindings(entry, roles);
            try
            {
                await SaveAccountListAsync();
            }
            catch
            {
                entry.GameRoles = oldRoles;
                entry.GameUid = oldUid;
                entry.GameRegion = oldRegion;
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SelectGameRoleAsync(string accountId, string uid, string region)
    {
        await _lock.WaitAsync();
        try
        {
            if (ActiveAccountId != accountId)
                throw new InvalidOperationException("社区账号已切换，请重新选择角色。");
            var entry = GetActiveAccountEntry()!;
            var oldUid = entry.GameUid;
            var oldRegion = entry.GameRegion;
            GameRoleSelection.Select(entry, uid, region);
            try
            {
                await SaveAccountListAsync();
            }
            catch
            {
                entry.GameUid = oldUid;
                entry.GameRegion = oldRegion;
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }
}