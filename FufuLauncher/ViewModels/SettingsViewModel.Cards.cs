/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 主页卡片与小部件

    partial void OnIsShowWidgetCardEnabledChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("IsShowWidgetCardEnabled", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }

    partial void OnShowWidgetGachaChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("ShowWidgetGacha", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }
    partial void OnShowWidgetAchievementChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("ShowWidgetAchievement", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }
    partial void OnShowWidgetInventoryChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("ShowWidgetInventory", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }
    partial void OnShowWidgetPlayerRoleChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("ShowWidgetPlayerRole", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }
    partial void OnShowWidgetDailyNoteWindowChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("ShowWidgetDailyNoteWindow", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }
    partial void OnShowWidgetVideoChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("ShowWidgetVideo", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }
    partial void OnShowWidgetBBSChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("ShowWidgetBBS", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }

    partial void OnIsShowPresetCardEnabledChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("IsShowPresetCardEnabled", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }

    private void CheckAndLimitDailyNoteItems(string settingName, Action revertAction)
    {
        if (_isUpdatingDailyNote) return;

        int activeCount = 0;
        if (ShowDailyNoteResin) activeCount++;
        if (ShowDailyNoteDailyTasks) activeCount++;
        if (ShowDailyNoteHomeCoin) activeCount++;
        if (ShowDailyNoteExpeditions) activeCount++;
        if (ShowDailyNoteTransformer) activeCount++;

        if (activeCount > 3)
        {
            _isUpdatingDailyNote = true;
            revertAction();
            _isUpdatingDailyNote = false;
            return;
        }

        var propertyValue = settingName switch
        {
            "ShowDailyNoteResin" => ShowDailyNoteResin,
            "ShowDailyNoteDailyTasks" => ShowDailyNoteDailyTasks,
            "ShowDailyNoteHomeCoin" => ShowDailyNoteHomeCoin,
            "ShowDailyNoteExpeditions" => ShowDailyNoteExpeditions,
            "ShowDailyNoteTransformer" => ShowDailyNoteTransformer,
            _ => false
        };

        _ = _localSettingsService.SaveSettingAsync(settingName, propertyValue);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }

    partial void OnShowDailyNoteResinChanged(bool value) => CheckAndLimitDailyNoteItems("ShowDailyNoteResin", () => ShowDailyNoteResin = false);
    partial void OnShowDailyNoteDailyTasksChanged(bool value) => CheckAndLimitDailyNoteItems("ShowDailyNoteDailyTasks", () => ShowDailyNoteDailyTasks = false);
    partial void OnShowDailyNoteHomeCoinChanged(bool value) => CheckAndLimitDailyNoteItems("ShowDailyNoteHomeCoin", () => ShowDailyNoteHomeCoin = false);
    partial void OnShowDailyNoteExpeditionsChanged(bool value) => CheckAndLimitDailyNoteItems("ShowDailyNoteExpeditions", () => ShowDailyNoteExpeditions = false);
    partial void OnShowDailyNoteTransformerChanged(bool value) => CheckAndLimitDailyNoteItems("ShowDailyNoteTransformer", () => ShowDailyNoteTransformer = false);

    partial void OnIsHideGameNewsCardEnabledChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("IsHideGameNewsCardEnabled", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }

    partial void OnIsHideCheckinCardEnabledChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("IsHideCheckinCardEnabled", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }

    partial void OnIsHideDailyNoteCardEnabledChanged(bool value)
    {
        _ = _localSettingsService.SaveSettingAsync("IsHideDailyNoteCardEnabled", value);
        WeakReferenceMessenger.Default.Send(new CardVisibilityChangedMessage());
    }

    #endregion
}
