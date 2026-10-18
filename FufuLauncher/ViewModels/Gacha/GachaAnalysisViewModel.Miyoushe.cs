/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Helpers;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisViewModel
{
    [RelayCommand]
    private async Task FetchFromMiYouSheAsync(bool incremental)
    {
        if (IsFetching || IsScraping) return;
        IsFetching = true;
        CrawlerStatus = "GachaAnalysis_ReadingBoundRoles".GetLocalized();
        try
        {
            var targetUid = _currentUid;
            if (string.IsNullOrEmpty(targetUid))
            {
                var current = await RoleService.GetCurrentAsync(RoleSelection);
                targetUid = current?.Role.game_uid;
            }

            if (string.IsNullOrEmpty(targetUid))
                throw new InvalidOperationException("Gacha_SelectRoleInSwitch".GetLocalized());
            var resolver = new GachaAccountResolver(async id => await _accountManager.LoadCookiesAsync(id),
                _gachaService.GetBoundRolesAsync);
            var result = await resolver.ResolveAsync(_accountManager.GetAllAccounts().ToList(),
                _accountManager.ActiveAccountId, targetUid);
            if (result.Roles.Count == 0)
            {
                CrawlerStatus = string.IsNullOrEmpty(targetUid)
                    ? "Gacha_NoBoundRoleFound".GetLocalized()
                    : string.Format("Gacha_NoAccountBoundToUid_Format".GetLocalized(), targetUid);
                if (result.Errors.Count > 0) CrawlerStatus += "\n" + string.Join("\n", result.Errors);
                OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            var selected = result.Roles[0];
            var cookies = selected.Cookies;
            CrawlerStatus = incremental ? "Gacha_GenAuthKeyIncremental".GetLocalized() : "Gacha_GenAuthKeyFull".GetLocalized();
            var link = await _gachaService.GenerateAuthKeyAsync(cookies, selected.Role);
            await FetchFromLinkAsync(link, selected.Role.game_uid, incremental, switchToSelectedRole: true);
        }
        catch (GachaAuthKeyException ex)
        {
            if (ex.RequiresReLogin)
            {
                CrawlerStatus = ex.Message;
                if (OnRequireReLoginAsync != null) await OnRequireReLoginAsync(CrawlerStatus);
                else OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            CrawlerStatus = ex.ReturnCode switch
            {
                1002 => "Gacha_AuthCode1002".GetLocalized(),
                1016 => "Gacha_AuthCode1016".GetLocalized(),
                _ => ex.Message + "Gacha_AuthCheckLoginSuffix".GetLocalized()
            };
            OnErrorAction?.Invoke(CrawlerStatus);
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
}
