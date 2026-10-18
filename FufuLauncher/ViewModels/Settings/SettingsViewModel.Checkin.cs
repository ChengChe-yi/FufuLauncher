/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Models;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 自动签到与云游戏

    private CheckinOptions _defaultCheckinOptions = new();
    private bool _isApplyingCheckinOptions;

    public bool CanEditCheckinOptions => !IsBatchCheckinEnabled || SelectedCheckinAccount != null;
    public bool CanEditCheckinGames => CanEditCheckinOptions && IsGameCheckinEnabled;

    public string CheckinOptionsCaption => SelectedCheckinAccount is { } account
        ? string.Format("Checkin_ConfiguringAccount".GetLocalized(), account.Nickname, account.Uid)
        : "Checkin_SelectAccount".GetLocalized();

    partial void OnIsCaptchaPopupDisabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsCaptchaPopupDisabled", value);
    }

    partial void OnIsCaptchaNoticeEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsCaptchaNoticeEnabled", value);
    }

    partial void OnIsAutoCheckinEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        Debug.WriteLine($"SettingsViewModel: 自动签到设置变更为 {value}");
        _ = _localSettingsService.SaveSettingAsync("IsAutoCheckinEnabled", value);
    }

    partial void OnIsGameCheckinEnabledChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditCheckinGames));
        SaveCheckinOption("IsGameCheckinEnabled", value);
    }

    partial void OnIsCommunityCheckinEnabledChanged(bool value)
    {
        SaveCheckinOption("IsCommunityCheckinEnabled", value);
    }

    partial void OnIsCommunityLikeEnabledChanged(bool value)
    {
        SaveCheckinOption("IsCommunityLikeEnabled", value);
    }

    partial void OnIsCommunityReadEnabledChanged(bool value)
    {
        SaveCheckinOption("IsCommunityReadEnabled", value);
    }

    partial void OnIsCommunityShareEnabledChanged(bool value)
    {
        SaveCheckinOption("IsCommunityShareEnabled", value);
    }

    partial void OnIsCloudGameCheckinEnabledChanged(bool value)
    {
        SaveCheckinOption("IsCloudGameCheckinEnabled", value);
    }

    partial void OnIsBatchCheckinEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsBatchCheckinEnabled", value);
        ApplyCheckinOptions();
    }

    partial void OnSelectedCheckinAccountChanged(CheckinAccountItem? value) => ApplyCheckinOptions();

    private void SaveCheckinOption(string key, bool value)
    {
        if (_isInitializing || _isApplyingCheckinOptions) return;
        var options = CaptureCheckinOptions();
        if (IsBatchCheckinEnabled)
        {
            if (SelectedCheckinAccount is not { } account) return;
            account.Options = options;
            _ = new CheckinSettingsStore(_localSettingsService).SaveAccountAsync(account.AccountId, options);
        }
        else
        {
            _defaultCheckinOptions = options;
            _ = _localSettingsService.SaveSettingAsync(key, value);
        }
    }

    private CheckinOptions CaptureCheckinOptions() => new()
    {
        IsGameCheckinEnabled = IsGameCheckinEnabled,
        IsCommunityCheckinEnabled = IsCommunityCheckinEnabled,
        IsCommunityLikeEnabled = IsCommunityLikeEnabled,
        IsCommunityReadEnabled = IsCommunityReadEnabled,
        IsCommunityShareEnabled = IsCommunityShareEnabled,
        IsCloudGameCheckinEnabled = IsCloudGameCheckinEnabled,
        Games = CheckinGames.ToDictionary(g => g.GameBiz, g => g.IsSelected)
    };

    private void ApplyCheckinOptions()
    {
        var options = IsBatchCheckinEnabled
            ? SelectedCheckinAccount?.Options ?? _defaultCheckinOptions
            : _defaultCheckinOptions;
        _isApplyingCheckinOptions = true;
        try
        {
            IsGameCheckinEnabled = options.IsGameCheckinEnabled;
            IsCommunityCheckinEnabled = options.IsCommunityCheckinEnabled;
            IsCommunityLikeEnabled = options.IsCommunityLikeEnabled;
            IsCommunityReadEnabled = options.IsCommunityReadEnabled;
            IsCommunityShareEnabled = options.IsCommunityShareEnabled;
            IsCloudGameCheckinEnabled = options.IsCloudGameCheckinEnabled;
            foreach (var game in CheckinGames) game.IsSelected = options.IsGameEnabled(game.GameBiz);
        }
        finally
        {
            _isApplyingCheckinOptions = false;
        }
    }

    private async Task LoadCheckinSectionAsync()
    {
        var store = new CheckinSettingsStore(_localSettingsService);
        _defaultCheckinOptions = await store.LoadDefaultsAsync();
        IsAutoCheckinEnabled = await store.ReadBoolAsync("IsAutoCheckinEnabled", false);
        IsBatchCheckinEnabled = await store.ReadBoolAsync("IsBatchCheckinEnabled", false);
        LoadCheckinGames();
        await LoadCheckinAccountsAsync();
        ApplyCheckinOptions();
    }

    private void LoadCheckinGames()
    {
        var games = new ObservableCollection<CheckinGameItem>();
        string[] gameOrder = ["nap_cn", "hkrpg_cn", "nxx_cn", "bh3_cn", "bh2_cn"];
        foreach (var gameBiz in gameOrder)
        {
            var game = MiyousheCheckinGame.SupportedGames.First(g => g.GameBiz == gameBiz);
            var item = new CheckinGameItem(game)
            {
                IsSelected = _defaultCheckinOptions.IsGameEnabled(gameBiz)
            };
            item.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(CheckinGameItem.IsSelected))
                    SaveCheckinOption(item.SettingKey, item.IsSelected);
            };
            games.Add(item);
        }

        CheckinGames = games;
    }

    private async Task LoadCheckinAccountsAsync()
    {
        try
        {
            var disabledUidsJson = await _localSettingsService.ReadSettingAsync("CheckinDisabledUids");
            var disabledUids = new HashSet<string>();
            if (disabledUidsJson != null)
            {
                try
                {
                    var list = JsonSerializer.Deserialize<List<string>>(disabledUidsJson.ToString() ?? "[]");
                    if (list != null) disabledUids = new HashSet<string>(list);
                }
                catch
                {
                }
            }

            var accounts = new ObservableCollection<CheckinAccountItem>();
            var store = new CheckinSettingsStore(_localSettingsService);
            var entries = _accountManager.GetAllAccounts();
            foreach (var entry in entries)
            {
                var cookies = await _accountManager.LoadCookiesAsync(entry.Id);
                if (cookies == null || cookies.Count == 0) continue;

                string uid = entry.Stuid;
                string nickname = entry.Nickname ?? string.Format("Account_User_Format".GetLocalized(), uid);

                string cloudTokenKey = $"CloudComboToken_{uid}";
                var cloudTokenObj = await _localSettingsService.ReadSettingAsync(cloudTokenKey);
                bool hasCloudCredential = !string.IsNullOrEmpty(cloudTokenObj?.ToString());

                accounts.Add(new CheckinAccountItem
                {
                    AccountId = entry.Id,
                    Options = await store.LoadAccountAsync(entry.Id),
                    Uid = uid,
                    Nickname = nickname,
                    IsSelected = !disabledUids.Contains(uid),
                    HasCloudCredential = hasCloudCredential
                });
            }

            var selectedId = SelectedCheckinAccount?.AccountId ?? _accountManager.ActiveAccountId;
            CheckinAccounts = new ObservableCollection<CheckinAccountItem>(
                CheckinSettingsStore.ApplyOrder(accounts, await store.LoadOrderAsync(), a => a.AccountId));
            SelectedCheckinAccount = CheckinAccounts.FirstOrDefault(a => a.AccountId == selectedId)
                                     ?? CheckinAccounts.FirstOrDefault();

            foreach (var account in CheckinAccounts)
            {
                account.PropertyChanged += async (s, e) =>
                {
                    if (e.PropertyName == nameof(CheckinAccountItem.IsSelected))
                    {
                        var disabled = CheckinAccounts.Where(a => !a.IsSelected).Select(a => a.Uid).ToList();
                        await _localSettingsService.SaveSettingAsync("CheckinDisabledUids",
                            JsonSerializer.Serialize(disabled));
                    }
                };
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadCheckinAccountsAsync 异常: {ex.Message}");
        }
    }

    public Task SaveCheckinAccountOrderAsync() => new CheckinSettingsStore(_localSettingsService)
        .SaveOrderAsync(CheckinAccounts.Select(a => a.AccountId));

    public static async Task SaveCloudCredentialAsync(string uid, string credential)
    {
        try
        {
            var localSettings = App.GetService<ILocalSettingsService>();
            string key = $"CloudComboToken_{uid}";
            await localSettings.SaveSettingAsync(key, credential);

            WeakReferenceMessenger.Default.Send(new CloudCredentialUpdatedMessage(uid));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存云游戏凭证失败: {ex.Message}");
        }
    }

    public async Task RemoveCloudCredentialAsync(string uid)
    {
        try
        {
            string key = $"CloudComboToken_{uid}";
            await _localSettingsService.RemoveSettingAsync(key);

            var account = CheckinAccounts?.FirstOrDefault(a => a.Uid == uid);
            if (account != null)
            {
                account.HasCloudCredential = false;
            }

            WeakReferenceMessenger.Default.Send(new CloudCredentialUpdatedMessage(uid));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"移除云游戏凭证失败: {ex.Message}");
        }
    }

    #endregion
}