/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Services;
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.ViewModels;

public partial class MainViewModel
{
    #region 游戏签到
    private bool _hasAttemptedAutoCheckin = false;
    private bool _isInternationalAccount = false;

    [ObservableProperty] private string _checkinStatusText = "Checkin_LoadingStatus".GetLocalized();
    [ObservableProperty] private bool _isCheckinButtonEnabled = true;
    [ObservableProperty] private string _checkinButtonText = "Checkin_SignNow".GetLocalized();
    [ObservableProperty] private string _checkinSummary = "";

    [ObservableProperty] private string _checkinStateGlyph = "\uE730";
    [ObservableProperty] private SolidColorBrush _checkinStateBrush = new(Microsoft.UI.Colors.Gray);
    [ObservableProperty] private string _checkinStateTooltip = "\u6E38\u620F\u7B7E\u5230\u72B6\u6001\u52A0\u8F7D\u4E2D";

    public IAsyncRelayCommand ExecuteCheckinCommand
    {
        get;
    }

    private void UpdateCheckinIconState(string statusText)
    {
        bool isSigned = !string.IsNullOrEmpty(statusText) &&
                        (statusText.Contains("成功") || statusText.Contains("已"));

        if (isSigned)
        {
            CheckinStateGlyph = "";
            CheckinStateBrush = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
            CheckinStateTooltip = "Checkin_Signed".GetLocalized();
        }
        else
        {
            CheckinStateGlyph = "";
            CheckinStateBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray) { Opacity = 0.8 };
            CheckinStateTooltip = "Checkin_Unsigned".GetLocalized();
        }

        IsCheckinButtonEnabled = true;
        CheckinButtonText = "Checkin_SignNow".GetLocalized();
    }

    private async Task LoadCheckinStatusAsync()
    {
        if (_localSettingsService == null) return;

        var isIntlRaw = await _localSettingsService.ReadSettingAsync("IsInternationalAccount");
        _isInternationalAccount = isIntlRaw != null && isIntlRaw.ToString().ToLower() == "true";

        try
        {
            var targetUidObj = await _localSettingsService.ReadSettingAsync("CustomCheckinUid");
            string targetUid = targetUidObj?.ToString();


            var accountManager = App.GetService<AccountManager>();
            var activeId = accountManager.ActiveAccountId;
            if (activeId == null)
            {
                CheckinStatusText = "Checkin_NotLoggedIn".GetLocalized();
                CheckinSummary = "Checkin_PleaseLogin".GetLocalized();
                UpdateCheckinIconState("Fail");
                return;
            }

            var cookies = await accountManager.LoadCookiesAsync(activeId);
            var entry = accountManager.GetActiveAccountEntry();
            if (cookies == null || entry == null)
            {
                CheckinStatusText = "Checkin_CredentialFailed".GetLocalized();
                CheckinSummary = "Checkin_CredentialUnavailable".GetLocalized();
                UpdateCheckinIconState("Fail");
                return;
            }

            string serverType = entry.ServerType;

            var (status, summary) = await _checkinService.GetCheckinStatusAsync(targetUid, cookies, serverType);

            CheckinStatusText = status;
            CheckinSummary = summary;
            UpdateCheckinIconState(status);

            if (!_hasAttemptedAutoCheckin)
            {
                var autoCheckinObj = await _localSettingsService.ReadSettingAsync("IsAutoCheckinEnabled");
                bool isAutoCheckinEnabled = autoCheckinObj != null && Convert.ToBoolean(autoCheckinObj);
                bool isSigned = !string.IsNullOrEmpty(status) && (status.Contains("成功") || status.Contains("已"));

                if (isAutoCheckinEnabled && !isSigned)
                {
                    _hasAttemptedAutoCheckin = true;
                    await ExecuteCheckinAsync();
                }
            }
        }
        catch (Exception ex)
        {
            CheckinStatusText = "Checkin_LoadFailed".GetLocalized();
            CheckinSummary = ex.Message;
            UpdateCheckinIconState("Fail");
        }
    }

    private async Task ExecuteCheckinAsync()
    {
        IsCheckinButtonEnabled = false;
        CheckinButtonText = "Checkin_CheckingIn".GetLocalized();
        CheckinStatusText = "Checkin_CheckingIn".GetLocalized();
        CheckinSummary = "Checkin_Executing".GetLocalized();

        //await RefreshSettingsAsync();

        try
        {
            var progress = new Progress<string>(msg =>
            {
                _dispatcherQueue.TryEnqueue(() =>
                {
                    CheckinButtonText = "Checkin_CheckingIn".GetLocalized();
                    CheckinSummary = msg;
                });
            });

            var unifiedResult = await _unifiedCheckinService.ExecuteAllCheckinsAsync(progress);

            CheckinStatusText = unifiedResult.OverallSuccess ? "Checkin_Complete".GetLocalized() : "Checkin_PartialFailed".GetLocalized();
            CheckinSummary = unifiedResult.SummaryMessage;
            UpdateCheckinIconState(unifiedResult.OverallSuccess ? "已签到" : "Fail");

            var notificationTitle = unifiedResult.NotificationType switch
            {
                NotificationType.Success => "Checkin_Complete".GetLocalized(),
                NotificationType.Warning => "Checkin_PartialFailed".GetLocalized(),
                _ => "Account_CheckinFailed".GetLocalized()
            };
            _notificationService.Show(notificationTitle, unifiedResult.GetDetailedSummary(), unifiedResult.NotificationType, 5000);
        }
        catch (Exception ex)
        {
            CheckinStatusText = "Checkin_ExecuteFailed".GetLocalized();
            CheckinSummary = ex.Message;
            UpdateCheckinIconState("Fail");
            _notificationService.Show("Account_CheckinException".GetLocalized(), ex.Message, NotificationType.Error, 3000);
        }
        finally
        {
            await Task.Delay(2000);
            await LoadCheckinStatusAsync();
        }
    }
    #endregion
}
