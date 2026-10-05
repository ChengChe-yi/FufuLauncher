/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Helpers;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisModel
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
                throw new InvalidOperationException("请先在角色切换开关中选择游戏角色。");
            var resolver = new GachaAccountResolver(async id => await _accountManager.LoadCookiesAsync(id),
                _gachaService.GetBoundRolesAsync);
            var result = await resolver.ResolveAsync(_accountManager.GetAllAccounts().ToList(),
                _accountManager.ActiveAccountId, targetUid);
            if (result.Roles.Count == 0)
            {
                CrawlerStatus = string.IsNullOrEmpty(targetUid)
                    ? "未找到可用于祈愿获取的原神绑定角色。请登录绑定了目标角色的米游社账号，或使用「从游戏获取」。"
                    : $"未找到绑定 UID {targetUid} 的米游社账号。请确认目标角色已绑定到已登录的米游社账号，或使用「从游戏获取」。";
                if (result.Errors.Count > 0) CrawlerStatus += "\n" + string.Join("\n", result.Errors);
                OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            var selected = result.Roles[0];
            var cookies = selected.Cookies;
            CrawlerStatus = incremental ? "正在生成认证密钥（增量更新）..." : "正在生成认证密钥（全量更新）...";
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
                1002 => "米游社未接受所选角色的认证参数（返回码 1002）。可使用「从游戏获取」读取游戏生成的链接。",
                1016 => "米游社未授权此账号获取所选游戏角色的祈愿记录（返回码 1016）。请检查绑定关系，或使用「从游戏获取」。",
                _ => ex.Message + " 请检查米游社登录状态，或使用「从游戏获取」。"
            };
            OnErrorAction?.Invoke(CrawlerStatus);
        }
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
}
