/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using MihoyoBBS;

namespace FufuLauncher.Services;

public class HoyoverseCheckinService : IHoyoverseCheckinService
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IHoyolabRoleResolverService _hoyolabRoleResolverService;

    public HoyoverseCheckinService(
        ILocalSettingsService localSettingsService,
        IHoyolabRoleResolverService hoyolabRoleResolverService)
    {
        _localSettingsService = localSettingsService;
        _hoyolabRoleResolverService = hoyolabRoleResolverService;
    }

    private static Config BuildConfigFromCookies(Dictionary<string, string> cookies, string serverType)
    {
        string cookieStr = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
        var config = new Config
        {
            Account = new MihoyoBBS.AccountConfig 
            {
                Cookie = cookieStr
            }
        };
        // 提取 stuid
        if (serverType == "os")
        {
            cookies.TryGetValue("ltuid_v2", out var stuid);
            config.Account.Stuid = stuid;
        }
        else
        {
            cookies.TryGetValue("stuid", out var stuid1);
            cookies.TryGetValue("ltuid", out var stuid2);
            config.Account.Stuid = stuid1 ?? stuid2;
        }
       
        cookies.TryGetValue("stoken", out var stoken);
        config.Account.Stoken = stoken;
        cookies.TryGetValue("mid", out var mid);
        config.Account.Mid = mid;
        return config;
    }
    public async Task<List<string>> GetBoundUidsAsync(Dictionary<string, string> cookies, string serverType)
    {
        if (serverType == "os")
        {
            string cookieStr = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
            var rolesResult = await _hoyolabRoleResolverService.ResolveRolesAsync(cookieStr);
            return rolesResult.Roles.Select(a => a.game_uid).ToList();
        }

        var config = BuildConfigFromCookies(cookies, serverType);
        if (!config.Games.Cn.Enable) return new List<string>();
        var genshin = new Genshin();
        await genshin.InitializeAsync(config).ConfigureAwait(false);
        return genshin.AccountList.Select(a => a.GameUid).ToList();
    }

    private async Task<HashSet<string>> GetDisabledUidsAsync()
    {
        var disabledUidsJson = await _localSettingsService.ReadSettingAsync("CheckinDisabledUids");
        if (disabledUidsJson != null)
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<string>>(disabledUidsJson.ToString() ?? "[]");
                if (list != null) return new HashSet<string>(list);
            }
            catch { }
        }
        return new HashSet<string>();
    }

    public async Task<(string status, string summary)> GetCheckinStatusAsync(string targetUid, Dictionary<string, string> cookies, string serverType)
    {
        if (serverType == "os")
        {
            string cookieStr = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
            var rolesResult = await _hoyolabRoleResolverService.ResolveRolesAsync(cookieStr);
            if (!rolesResult.HasRoles)
                return ("Checkin_NotDetected".GetLocalized(), rolesResult.Message);

            var role = SelectRole(rolesResult.Roles, targetUid);
            var os = new HoyolabCheckinService();
            await os.InitializeAsync(cookieStr, rolesResult.Roles.Select(ToOsAccountItem).ToList());

            var isData = await os.IsSignAsync(role.region, role.game_uid);
            if (isData == null)
                return ("Checkin_GetStatusFailed".GetLocalized(), HoyolabCheckinService.LastApiError);
            return (isData.IsSign ? "Checkin_SignedToday".GetLocalized() : "Checkin_UnsignedToday".GetLocalized(), $"HoYoLAB: {role.nickname}");
        }

        var config = BuildConfigFromCookies(cookies, serverType);
        if (!config.Games.Cn.Enable || !config.Games.Cn.Genshin.Checkin)
            return ("Checkin_NotEnabled".GetLocalized(), "Checkin_EnableInConfig".GetLocalized());
        var genshin = new Genshin();
        await genshin.InitializeAsync(config);
        if (genshin.AccountList.Count == 0)
            return ("Checkin_NotDetectedAccount".GetLocalized(), GameCheckin.LastApiError);
        var cnAccount = string.IsNullOrEmpty(targetUid)
            ? genshin.AccountList[0]
            : genshin.AccountList.FirstOrDefault(a => a.GameUid == targetUid) ?? genshin.AccountList[0];
        var isSignData = await genshin.IsSignAsync(cnAccount.Region, cnAccount.GameUid, false);
        if (isSignData == null)
            return ("Checkin_GetStatusFailed".GetLocalized(), GameCheckin.LastApiError);
        return (isSignData.IsSign == true ? "Checkin_SignedToday".GetLocalized() : "Checkin_UnsignedToday".GetLocalized(), string.Format("Checkin_AccountLabel".GetLocalized(), cnAccount.Nickname));
    }

    public async Task<(bool success, string message)> ExecuteCheckinAsync(string targetUid, Dictionary<string, string> cookies, string serverType)
    {
        if (serverType == "os")
        {
            string cookieStr = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
            var rolesResult = await _hoyolabRoleResolverService.ResolveRolesAsync(cookieStr);
            if (!rolesResult.HasRoles)
                return (false, rolesResult.Message);

            var os = new HoyolabCheckinService();
            await os.InitializeAsync(cookieStr, rolesResult.Roles.Select(ToOsAccountItem).ToList());
            var osResult = await os.SignAccountWithResultAsync(cookieStr, new HashSet<string>(), targetUid);
            return (osResult.Success, osResult.Message);
        }

        var config = BuildConfigFromCookies(cookies, serverType);
        if (!config.Games.Cn.Enable || !config.Games.Cn.Genshin.Checkin)
            return (false, "Checkin_FeatureNotEnabled".GetLocalized());
        var genshin = new Genshin();
        await genshin.InitializeAsync(config);
        var disabledUids = await GetDisabledUidsAsync();
        string result = await genshin.SignAccountAsync(config, targetUid, disabledUids);
        var failKey = "Status_Failure".GetLocalized();
        var excKey = "Status_Exception".GetLocalized();
        bool success = !result.Contains(failKey) && !result.Contains(excKey);
        return (success, result);
    }

    public async Task<CheckinCalendarData?> GetCalendarDataAsync(Dictionary<string, string> cookies, string serverType)
    {
        if (serverType == "os")
        {
            string cookieStr = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
            var rolesResult = await _hoyolabRoleResolverService.ResolveRolesAsync(cookieStr);
            var os = new HoyolabCheckinService();
            await os.InitializeAsync(cookieStr, rolesResult.Roles.Select(ToOsAccountItem).ToList());
            return await os.GetCheckinCalendarAsync();
        }

        var config = BuildConfigFromCookies(cookies, serverType);
        var genshin = new Genshin();
        await genshin.InitializeAsync(config);
        return await genshin.GetCheckinCalendarAsync();
    }

    public async Task<CheckinResignInfo?> GetResignInfoAsync(string targetUid, Dictionary<string, string> cookies, string serverType)
    {
        if (serverType == "os")
        {
            string cookieStr = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
            var rolesResult = await _hoyolabRoleResolverService.ResolveRolesAsync(cookieStr);
            if (!rolesResult.HasRoles)
                return null;

            var role = SelectRole(rolesResult.Roles, targetUid);
            var os = new HoyolabCheckinService();
            await os.InitializeAsync(cookieStr, rolesResult.Roles.Select(ToOsAccountItem).ToList());
            return await os.GetResignInfoAsync(role.region, role.game_uid);
        }
        
        var config = BuildConfigFromCookies(cookies, serverType);
        var genshin = new Genshin();
        await genshin.InitializeAsync(config);
        var account = SelectCnAccount(genshin.AccountList, targetUid);
        if (account == null)
            return null;

        return await genshin.GetResignInfoAsync(account.Region, account.GameUid);
    }

    public async Task<(bool success, string message)> ExecuteResignAsync(string targetUid, Dictionary<string, string> cookies, string serverType)
    {
        if (serverType == "os")
        {
            string cookieStr = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
            var rolesResult = await _hoyolabRoleResolverService.ResolveRolesAsync(cookieStr);
            if (!rolesResult.HasRoles)
                return (false, rolesResult.Message);

            var role = SelectRole(rolesResult.Roles, targetUid);
            var os = new HoyolabCheckinService();
            await os.InitializeAsync(cookieStr, rolesResult.Roles.Select(ToOsAccountItem).ToList());
            return await os.ResignAsync(role.region, role.game_uid);
        }
        
        var config = BuildConfigFromCookies(cookies, serverType);
        var genshin = new Genshin();
        await genshin.InitializeAsync(config);
        var account = SelectCnAccount(genshin.AccountList, targetUid);
        if (account == null)
            return (false, "Checkin_NoBoundAccount".GetLocalized());

        var (success, message, retcode) = await genshin.ResignAsync(account.Region, account.GameUid);
        if (success)
            return (true, message);

        string mapped = retcode switch
        {
            -5003 => "Checkin_AlreadySignedToday".GetLocalized(),
            -5005 => "Checkin_ResignLimitExceeded".GetLocalized(),
            -5007 => "Checkin_ResignNotSigned".GetLocalized(),
            -5008 => "Checkin_ResignNoDate".GetLocalized(),
            -5014 => "Checkin_ResignInsufficientCoinCn".GetLocalized(),
            _ => string.Format("Checkin_ResignFailed".GetLocalized(), retcode, message)
        };
        return (false, mapped);
    }

    private static AccountItem? SelectCnAccount(List<AccountItem> accounts, string targetUid)
    {
        if (accounts.Count == 0)
            return null;

        return string.IsNullOrEmpty(targetUid)
            ? accounts[0]
            : accounts.FirstOrDefault(a => a.GameUid == targetUid) ?? accounts[0];
    }

    private static GameRoleInfo SelectRole(List<GameRoleInfo> roles, string targetUid)
    {
        if (string.IsNullOrWhiteSpace(targetUid))
            return roles[0];

        return roles.FirstOrDefault(r => r.game_uid == targetUid) ?? roles[0];
    }

    private static OsAccountItem ToOsAccountItem(GameRoleInfo role)
    {
        return new OsAccountItem
        {
            GameUid = role.game_uid,
            Region = role.region,
            Nickname = role.nickname
        };
    }
}

