/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;

namespace FufuLauncher.ViewModels;

public partial class GachaAnalysisModel
{
    #region Gacha Record Fetching

    [RelayCommand]
    private async Task FetchFromMiYouSheAsync(bool incremental)
    {
        var loggedInAccount = _accountManager.GetActiveAccountEntry();
        var loggedInUid = loggedInAccount?.GameUid ?? "";

        string gameUid = _currentUid;
        if (string.IsNullOrEmpty(gameUid))
            gameUid = loggedInUid;

        if (string.IsNullOrEmpty(gameUid))
        {
            CrawlerStatus = "当前账号未绑定游戏角色，无法获取祈愿记录";
            OnErrorAction?.Invoke(CrawlerStatus);
            return;
        }

        var candidates = _accountManager.GetAllAccounts()
            .Where(a => a.ServerType == "cn" && a.GameUid == gameUid)
            .ToList();

        var matchedAccount = candidates.FirstOrDefault(a => a.Id == _accountManager.ActiveAccountId)
            ?? candidates.FirstOrDefault();

        if (matchedAccount == null && loggedInAccount != null && loggedInUid == gameUid)
        {
            matchedAccount = loggedInAccount;
        }

        if (matchedAccount == null)
        {
            var activeId = _accountManager.ActiveAccountId;
            if (activeId == null)
            {
                CrawlerStatus = $"未找到绑定 UID {gameUid} 的米游社账号，请先登录后重试";
                OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            if (!string.IsNullOrEmpty(loggedInUid) && loggedInUid != gameUid)
            {
                CrawlerStatus = $"当前登录账户 UID {loggedInUid} 与目标账户 UID {gameUid} 不一致，且未找到绑定 UID {gameUid} 的米游社账号，请登录到目标账户";
                if (OnRequireReLoginAsync != null)
                    await OnRequireReLoginAsync($"当前登录的米游社账户为 UID {loggedInUid}，而你正在更新 UID {gameUid} 的记录。\n请先登录到 UID {gameUid} 对应的账户后再试。");
                else
                    OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            matchedAccount = loggedInAccount;
        }

        var accountLabel = string.IsNullOrWhiteSpace(matchedAccount.Nickname)
            ? matchedAccount.Stuid
            : matchedAccount.Nickname;

        var cookies = await _accountManager.LoadCookiesAsync(matchedAccount.Id);
        if (cookies == null || cookies.Count == 0)
        {
            CrawlerStatus = $"无法读取账号 {accountLabel} 的登录凭证，请重新登录该账号";
            OnErrorAction?.Invoke(CrawlerStatus);
            return;
        }


        string stoken = null, mid = null, stuid = null;
        cookies.TryGetValue("stoken", out stoken);
        cookies.TryGetValue("mid", out mid);
        cookies.TryGetValue("stuid", out stuid);
        if (string.IsNullOrEmpty(stuid))
            cookies.TryGetValue("ltuid", out stuid);

        if (string.IsNullOrEmpty(stoken) || string.IsNullOrEmpty(mid))
        {
            CrawlerStatus = $"账号 {accountLabel} 的登录凭证不完整（缺少 stoken/mid），请重新登录该账号";
            OnErrorAction?.Invoke(CrawlerStatus);
            return;
        }


        var previousUid = string.IsNullOrEmpty(_currentUid) ? _uidBeforeAddNew : _currentUid;
        _uidBeforeAddNew = "";
        _currentUid = gameUid;

        IsFetching = true;
        CrawlerStatus = incremental ? "正在生成认证密钥（增量更新）..." : "正在生成认证密钥（全量更新）...";

        try
        {
            var authkey = await _gachaService.GenerateAuthKeyAsync(stoken, mid, stuid, gameUid);

            if (string.IsNullOrEmpty(authkey))
            {
                CrawlerStatus = $"认证密钥生成失败，请重新登录账号 {accountLabel} 后重试";
                IsFetching = false;
                if (OnRequireReLoginAsync != null)
                    await OnRequireReLoginAsync($"UID {gameUid} 的认证密钥生成失败，账号 {accountLabel} 的登录凭证可能已过期。\n请重新登录该账号后再试。");
                else
                    OnErrorAction?.Invoke(CrawlerStatus);
                return;
            }

            var baseUrl = $"https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog?authkey={Uri.EscapeDataString(authkey)}&authkey_ver=1&sign_type=2&game=hk4e&lang=zh-cn";

            void OnProgress(string pool, int count) =>
                App.MainWindow.DispatcherQueue.TryEnqueue(() => CrawlerStatus = $"正在获取{pool}记录... (已获取 {count} 条)");

            long charEndId = incremental ? GetNewestLogId(_cachedCharacterLogs) : 0;
            long weaponEndId = incremental ? GetNewestLogId(_cachedWeaponLogs) : 0;
            long chronicledEndId = incremental ? GetNewestLogId(_cachedChronicledLogs) : 0;
            long noviceEndId = incremental ? GetNewestLogId(_cachedNoviceLogs) : 0;
            long standardEndId = incremental ? GetNewestLogId(_cachedStandardLogs) : 0;

            CrawlerStatus = "正在获取角色活动记录...";
            var charLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "301", count => OnProgress("角色活动", count), charEndId);
            foreach (var l in charLogs) l.Uid = gameUid;
            _cachedCharacterLogs = MergeLogs(_cachedCharacterLogs, charLogs);

            CrawlerStatus = $"角色活动 {charLogs.Count} 条，正在获取武器活动记录...";
            var weaponLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "302", count => OnProgress("武器活动", count), weaponEndId);
            foreach (var l in weaponLogs) l.Uid = gameUid;
            _cachedWeaponLogs = MergeLogs(_cachedWeaponLogs, weaponLogs);

            CrawlerStatus = $"武器活动 {weaponLogs.Count} 条，正在获取集录祈愿记录...";
            var chronicledLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "500", count => OnProgress("集录祈愿", count), chronicledEndId);
            foreach (var l in chronicledLogs) l.Uid = gameUid;
            _cachedChronicledLogs = MergeLogs(_cachedChronicledLogs, chronicledLogs);

            CrawlerStatus = $"集录祈愿 {chronicledLogs.Count} 条，正在获取新手祈愿记录...";
            var noviceLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "100", count => OnProgress("新手祈愿", count), noviceEndId);
            foreach (var l in noviceLogs) l.Uid = gameUid;
            _cachedNoviceLogs = MergeLogs(_cachedNoviceLogs, noviceLogs);

            CrawlerStatus = $"新手祈愿 {noviceLogs.Count} 条，正在获取常驻祈愿记录...";
            var standardLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "200", count => OnProgress("常驻祈愿", count), standardEndId);
            foreach (var l in standardLogs) l.Uid = gameUid;
            _cachedStandardLogs = MergeLogs(_cachedStandardLogs, standardLogs);

            FillMissingFieldsFromMetadata(charLogs, weaponLogs, chronicledLogs, noviceLogs, standardLogs);

            var total = charLogs.Count + weaponLogs.Count + chronicledLogs.Count + noviceLogs.Count + standardLogs.Count;

            if (total == 0)
            {
                var hasExistingRecords = _cachedCharacterLogs.Count + _cachedWeaponLogs.Count + _cachedChronicledLogs.Count + _cachedNoviceLogs.Count + _cachedStandardLogs.Count > 0;

                IsFetching = false;

                if (hasExistingRecords)
                {
                    CrawlerStatus = $"UID {gameUid} 未获取到新记录，已保留现有数据";
                    return;
                }

                _cachedCharacterLogs.Clear();
                _cachedWeaponLogs.Clear();
                _cachedChronicledLogs.Clear();
                _cachedNoviceLogs.Clear();
                _cachedStandardLogs.Clear();

                if (!string.IsNullOrEmpty(previousUid))
                {
                    if (OnShowConfirmDialogAsync != null)
                    {
                        await OnShowConfirmDialogAsync(
                            "无祈愿记录",
                            $"UID {gameUid} 没有获取到祈愿记录，已切回 UID {previousUid}",
                            "确定");
                    }
                    await SwitchToUidAsync(previousUid);
                }
                else
                {
                    if (OnShowConfirmDialogAsync != null)
                    {
                        await OnShowConfirmDialogAsync(
                            "无祈愿记录",
                            $"UID {gameUid} 没有获取到祈愿记录",
                            "确定");
                    }
                    await AddNewUserAsync();
                }
                return;
            }

            CrawlerStatus = $"获取完成，共 {total} 条记录，正在检查图片资源...";

            HasGachaData = true;
            SaveGachaDataAsync();

            IsScraping = true;
            if (RequestMetadataScrapeAction != null)
                RequestMetadataScrapeAction.Invoke();
            else
                RefreshUIFromCache();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Gacha] 获取异常: {ex}");
            CrawlerStatus = $"获取失败: {ex.Message}";
            IsFetching = false;
            OnErrorAction?.Invoke(CrawlerStatus);
        }

        if (!IsScraping) IsFetching = false;
    }

    [RelayCommand]
    private async Task FetchGachaDataAsync()
    {
        if (string.IsNullOrWhiteSpace(GachaUrl))
        {
            CrawlerStatus = "请输入有效的抽卡链接";
            return;
        }

        IsFetching = true;
        CrawlerStatus = "正在解析 API 链接...";

        try
        {
            var baseUrl = _gachaService.ExtractBaseUrl(GachaUrl);
            if (string.IsNullOrEmpty(baseUrl))
            {
                CrawlerStatus = "链接格式错误，无法提取 API 地址";
                IsFetching = false;
                return;
            }

            void OnProgress(string pool, int count) =>
                App.MainWindow.DispatcherQueue.TryEnqueue(() => CrawlerStatus = $"正在获取{pool}记录... (已获取 {count} 条)");

            CrawlerStatus = "正在获取角色活动记录...";
            var charLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "301", count => OnProgress("角色活动", count));

            CrawlerStatus = $"角色活动 {charLogs.Count} 条，正在获取武器活动记录...";
            var weaponLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "302", count => OnProgress("武器活动", count));

            CrawlerStatus = $"武器活动 {weaponLogs.Count} 条，正在获取集录祈愿记录...";
            var chronicledLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "500", count => OnProgress("集录祈愿", count));

            CrawlerStatus = $"集录祈愿 {chronicledLogs.Count} 条，正在获取新手祈愿记录...";
            var noviceLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "100", count => OnProgress("新手祈愿", count));

            CrawlerStatus = $"新手祈愿 {noviceLogs.Count} 条，正在获取常驻祈愿记录...";
            var standardLogs = await _gachaService.FetchGachaLogAsync(baseUrl, "200", count => OnProgress("常驻祈愿", count));

            var allFetched = charLogs.Concat(weaponLogs).Concat(chronicledLogs).Concat(noviceLogs).Concat(standardLogs).ToList();
            var fetchedUid = allFetched.FirstOrDefault(l => !string.IsNullOrEmpty(l.Uid))?.Uid ?? "";

            if (!await HandleUidMismatchAsync(fetchedUid)) { IsFetching = false; return; }

            _currentUid = fetchedUid;

            _cachedCharacterLogs = MergeLogs(_cachedCharacterLogs, charLogs);
            _cachedWeaponLogs = MergeLogs(_cachedWeaponLogs, weaponLogs);
            _cachedChronicledLogs = MergeLogs(_cachedChronicledLogs, chronicledLogs);
            _cachedNoviceLogs = MergeLogs(_cachedNoviceLogs, noviceLogs);
            _cachedStandardLogs = MergeLogs(_cachedStandardLogs, standardLogs);

            FillMissingFieldsFromMetadata(charLogs, weaponLogs, chronicledLogs, noviceLogs, standardLogs);

            var total = charLogs.Count + weaponLogs.Count + chronicledLogs.Count + noviceLogs.Count + standardLogs.Count;
            CrawlerStatus = $"获取完成，共 {total} 条记录，正在检查图片资源...";

            HasGachaData = true;
            SaveGachaDataAsync();

            IsScraping = true;
            if (RequestMetadataScrapeAction != null)
                RequestMetadataScrapeAction.Invoke();
            else
                RefreshUIFromCache();
        }
        catch (Exception ex)
        {
            CrawlerStatus = $"更新失败: {ex.Message}";
            IsFetching = false;
        }

        if (!IsScraping) IsFetching = false;
    }

    #endregion
}
