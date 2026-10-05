/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Contracts.Services;
using FufuLauncher.Models;
using FufuLauncher.Models.MiHoYo;

namespace FufuLauncher.Services;

public sealed record GachaAccountRole(AccountEntry Account, GameRoleInfo Role, Dictionary<string, string> Cookies);
public sealed record GachaAccountRolesResult(List<GachaAccountRole> Roles, List<string> Errors);

/// <summary>Resolve credentials from live bindings, rather than the account's first cached GameUid.</summary>
public sealed class GachaAccountResolver(
    Func<string, Task<Dictionary<string, string>?>> loadCookies,
    Func<string, Task<GameRolesResponse>> loadRoles)
{
    public static bool IsSupportedRole(GameRoleInfo role) =>
        role.game_biz == "hk4e_cn" &&
        role.region is ServerRegion.CnGf01 or ServerRegion.CnQd01 &&
        !string.IsNullOrEmpty(role.game_uid) && role.game_uid.Length is 9 or 10 &&
        role.game_uid.All(char.IsAsciiDigit) && ServerRegion.Resolve(role.game_uid) == role.region;

    public async Task<GachaAccountRolesResult> ResolveAsync(IEnumerable<AccountEntry> accounts,
        string? activeAccountId, string? targetUid = null)
    {
        var roles = new List<GachaAccountRole>();
        var errors = new List<string>();
        foreach (var account in accounts.Where(a => a.ServerType == "cn")
                     .OrderByDescending(a => a.Id == activeAccountId))
        {
            var label = string.IsNullOrWhiteSpace(account.Nickname) ? account.Stuid : account.Nickname;
            try
            {
                var cookies = await loadCookies(account.Id);
                if (cookies == null || cookies.Count == 0)
                {
                    errors.Add($"账号 {label} 的登录凭证无法读取，请重新登录该账号。");
                    continue;
                }

                var response = await loadRoles(string.Join("; ", cookies.Select(c => $"{c.Key}={c.Value}")));
                if (response?.retcode != 0 || response.data?.list == null)
                {
                    errors.Add($"账号 {label} 的绑定角色查询失败（返回码 {response?.retcode.ToString() ?? "未知"}）。");
                    continue;
                }

                var supportedRoles = response.data.list.Where(IsSupportedRole)
                    .DistinctBy(r => (r.game_uid, r.region));
                if (!string.IsNullOrEmpty(targetUid))
                    supportedRoles = supportedRoles.Where(r => r.game_uid == targetUid);
                roles.AddRange(supportedRoles.Select(r => new GachaAccountRole(account, r, cookies)));

                // For an existing archive, use only credentials verified to own that exact role.
                if (!string.IsNullOrEmpty(targetUid) && roles.Count > 0) break;
            }
            catch (Exception ex)
            {
                // Exception messages from cookie stores or transports may contain private data.
                errors.Add($"账号 {label} 的绑定角色查询未完成（{ex.GetType().Name}），请稍后重试。");
            }
        }
        return new GachaAccountRolesResult(roles, errors);
    }
}
