/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Helpers;
using FufuLauncher.Models.MiHoYo;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;

namespace FufuLauncher.ViewModels;

public partial class MainViewModel
{
    #region 每日便签
    [ObservableProperty] private int _currentResin;
    [ObservableProperty] private int _maxResin;
    [ObservableProperty] private string _resinRecoveryTime = "";
    [ObservableProperty] private int _finishedTaskNum;
    [ObservableProperty] private int _totalTaskNum;
    [ObservableProperty] private int _currentHomeCoin;
    [ObservableProperty] private int _maxHomeCoin;
    [ObservableProperty] private int _currentExpeditionNum;
    [ObservableProperty] private int _maxExpeditionNum;
    [ObservableProperty] private bool _isTransformerObtained;
    [ObservableProperty] private string _transformerRecoveryTime = "";

    [ObservableProperty] private Visibility _showResin = Visibility.Visible;
    [ObservableProperty] private Visibility _showDailyTasks = Visibility.Visible;
    [ObservableProperty] private Visibility _showHomeCoin = Visibility.Visible;
    [ObservableProperty] private Visibility _showExpeditions = Visibility.Visible;
    [ObservableProperty] private Visibility _showTransformer = Visibility.Visible;
    [ObservableProperty] private bool _isDailyNoteLoaded;
    [ObservableProperty] private string _dailyNoteStatus = "Home_NoData".GetLocalized();
    private int _dailyNoteVersion;

    public async Task LoadDailyNoteAsync(bool force = false)
    {
        // 便签卡片隐藏时不发起任何 API 请求
        var hideJson = await _localSettingsService.ReadSettingAsync("IsHideDailyNoteCardEnabled");
        if (hideJson != null && Convert.ToBoolean(hideJson))
        {
            IsDailyNoteLoaded = false;
            Debug.WriteLine("[DailyNote] 便签卡片已隐藏，跳过API请求");
            return;
        }

        if (!force && IsDailyNoteLoaded)
        {
            Debug.WriteLine("[DailyNote] 已有数据，跳过本次请求");
            return;
        }

        var version = Interlocked.Increment(ref _dailyNoteVersion);
        try
        {
            await ClearDailyNoteDataAsync(invalidate: false);
            var roles = App.GetService<GameRoleService>();
            var selected = await roles.GetCurrentAsync();
            if (version != _dailyNoteVersion || selected == null) return;
            var dailyNoteData = await _dailyNoteCardService.LoadCardDataAsync(
                selected.Role.game_uid, selected.Role.region, selected.AccountId);

            if (dailyNoteData == null)
            {
                Debug.WriteLine("[DailyNote] 登录过期且刷新失败，跳过便签更新");
                return;
            }

            await UpdateUI(() =>
            {
                if (version != _dailyNoteVersion || !roles.IsCurrent(selected)) return;
                CurrentResin = dailyNoteData.CurrentResin;
                MaxResin = dailyNoteData.MaxResin;
                FinishedTaskNum = dailyNoteData.FinishedTaskNum;
                TotalTaskNum = dailyNoteData.TotalTaskNum;
                CurrentHomeCoin = dailyNoteData.CurrentHomeCoin;
                MaxHomeCoin = dailyNoteData.MaxHomeCoin;
                CurrentExpeditionNum = dailyNoteData.CurrentExpeditionNum;
                MaxExpeditionNum = dailyNoteData.MaxExpeditionNum;
                IsTransformerObtained = dailyNoteData.IsTransformerObtained;
                TransformerRecoveryTime = dailyNoteData.TransformerRecoveryTime;

                IsDailyNoteLoaded = true;
            });

            Debug.WriteLine("[DailyNote] 便签数据加载成功");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DailyNote] 加载便签数据失败: {ex.Message}");
            await UpdateUI(() =>
            {
                if (version == _dailyNoteVersion) DailyNoteStatus = ex.Message;
            });
        }
    }

    private async Task ClearDailyNoteDataAsync(bool invalidate = true)
    {
        if (invalidate) Interlocked.Increment(ref _dailyNoteVersion);
        await UpdateUI(() =>
        {
            DailyNoteStatus = "Home_NoData".GetLocalized();
            CurrentResin = 0;
            MaxResin = 0;
            FinishedTaskNum = 0;
            TotalTaskNum = 0;
            CurrentHomeCoin = 0;
            MaxHomeCoin = 0;
            CurrentExpeditionNum = 0;
            MaxExpeditionNum = 0;
            IsTransformerObtained = false;
            TransformerRecoveryTime = "";
            IsDailyNoteLoaded = false;
        });
    }
    #endregion
}
