/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Models.GameAnnouncement;
using FufuLauncher.Services;
using FufuLauncher.Services.Background;
using FufuLauncher.Services.PluginMirror;

namespace FufuLauncher.ViewModels;

public partial class SettingsViewModel
{
    #region 设置加载

    public async Task ReloadSettingsAsync()
    {
        _isLoadingLaunchParams = true;
        _isInitializing = true;

        try
        {
            await LoadUserPreferencesAsync();
            await LoadCustomBackgroundSettingsAsync();
            await InitializeNavItemsAsync();
            
            OnPropertyChanged(nameof(IsStartupSoundEnabled));
            OnPropertyChanged(nameof(StartupSoundPath));
            OnPropertyChanged(nameof(HasCustomStartupSound));
            OnPropertyChanged(nameof(ElementTheme));
            OnPropertyChanged(nameof(SelectedServer));
            OnPropertyChanged(nameof(IsBackgroundEnabled));
            OnPropertyChanged(nameof(SelectedLanguage));
            OnPropertyChanged(nameof(MinimizeToTray));
            OnPropertyChanged(nameof(CustomLaunchParameters));
            OnPropertyChanged(nameof(LaunchArgsWindowMode));
            OnPropertyChanged(nameof(LaunchArgsWidth));
            OnPropertyChanged(nameof(LaunchArgsHeight));
            OnPropertyChanged(nameof(CustomBackgroundPath));
            OnPropertyChanged(nameof(HasCustomBackground));
            OnPropertyChanged(nameof(IsBackgroundSlideshowEnabled));
            OnPropertyChanged(nameof(BackgroundSlideshowFolder));
            OnPropertyChanged(nameof(HasBackgroundSlideshowFolder));
            OnPropertyChanged(nameof(BackgroundSlideshowInterval));
            OnPropertyChanged(nameof(CustomBackgroundApiUrl));
            OnPropertyChanged(nameof(CurrentBackgroundApiUrl));
            OnPropertyChanged(nameof(AppThemeColor));
            OnPropertyChanged(nameof(CurrentWindowBackdrop));
            OnPropertyChanged(nameof(IsShortTermSupportEnabled));
            OnPropertyChanged(nameof(IsBetterGIIntegrationEnabled));
            OnPropertyChanged(nameof(IsBetterGICloseOnExitEnabled));
            OnPropertyChanged(nameof(BetterGIStartupDelaySeconds));
            OnPropertyChanged(nameof(GlobalBackgroundOverlayOpacity));
            OnPropertyChanged(nameof(ContentFrameBackgroundOpacity));
            OnPropertyChanged(nameof(IsSaveWindowSizeEnabled));
            OnPropertyChanged(nameof(IsMinWindowSizeLimitEnabled));
            OnPropertyChanged(nameof(IsHideGameNewsCardEnabled));
            OnPropertyChanged(nameof(IsHideCheckinCardEnabled));
            OnPropertyChanged(nameof(IsAcrylicOverlayEnabled));
            OnPropertyChanged(nameof(IsAutoCheckinEnabled));
            OnPropertyChanged(nameof(AppProcessPriority));
            OnPropertyChanged(nameof(IsCpuUsageWarningEnabled));
            OnPropertyChanged(nameof(CpuUsageWarningThreshold));
            OnPropertyChanged(nameof(IsRedeemCodeNotificationEnabled));
            OnPropertyChanged(nameof(IsCaptchaPopupDisabled));
            LoadMonitors();
        }
        finally
        {
            _isLoadingLaunchParams = false;
            _isInitializing = false;
        }

        await LoadCheckinAccountsAsync();
    }

    private async Task LoadUserPreferencesAsync()
    {
        var serverJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.BackgroundServerKey);
        int serverValue = serverJson != null ? Convert.ToInt32(serverJson) : 0;
        SelectedServer = (ServerType)serverValue;
        var customBackgroundApiJson = await _localSettingsService.ReadSettingAsync("CustomBackgroundApiUrl");
        CustomBackgroundApiUrl = customBackgroundApiJson?.ToString() ?? string.Empty;
        CurrentBackgroundApiUrl = string.IsNullOrWhiteSpace(CustomBackgroundApiUrl)
            ? GetDefaultBackgroundApiUrl(SelectedServer)
            : CustomBackgroundApiUrl;

        var enabledJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.IsBackgroundEnabledKey);
        IsBackgroundEnabled = enabledJson == null ? true : Convert.ToBoolean(enabledJson);

        var languageJson = await _localSettingsService.ReadSettingAsync("AppLanguage");
        int languageValue = languageJson != null ? Convert.ToInt32(languageJson) : 0;
        SelectedLanguage = (AppLanguage)languageValue;

        var trayJson = await _localSettingsService.ReadSettingAsync("MinimizeToTray");
        MinimizeToTray = trayJson != null && Convert.ToBoolean(trayJson);
        
        var acrylicOverlayJson = await _localSettingsService.ReadSettingAsync("IsAcrylicOverlayEnabled");
        IsAcrylicOverlayEnabled = acrylicOverlayJson == null || Convert.ToBoolean(acrylicOverlayJson);

        var pageOverlaySemiTransparentJson = await _localSettingsService.ReadSettingAsync("IsPageOverlaySemiTransparentEnabled");
        IsPageOverlaySemiTransparentEnabled = pageOverlaySemiTransparentJson != null && Convert.ToBoolean(pageOverlaySemiTransparentJson);

        var pageOverlayTargetOpacityJson = await _localSettingsService.ReadSettingAsync("PageOverlayTargetOpacity");
        if (pageOverlayTargetOpacityJson != null && double.TryParse(pageOverlayTargetOpacityJson.ToString(), out var pageOverlayOpacity))
            PageOverlayTargetOpacity = Math.Clamp(pageOverlayOpacity, 0.1, 1.0);
        else
            PageOverlayTargetOpacity = 0.7;

        var hamburgerButtonJson = await _localSettingsService.ReadSettingAsync("IsHamburgerButtonEnabled");
        IsHamburgerButtonEnabled = hamburgerButtonJson != null && Convert.ToBoolean(hamburgerButtonJson);
        
        var launchOverlayColorJson = await _localSettingsService.ReadSettingAsync("LaunchButtonOverlayColor");
        LaunchButtonOverlayColor = launchOverlayColorJson?.ToString() ?? "#0078D7";

        var paramsJson = await _localSettingsService.ReadSettingAsync("CustomLaunchParameters");
        if (paramsJson != null)
        {
            CustomLaunchParameters = paramsJson.ToString();
            ParseLaunchParameters(CustomLaunchParameters);
        }

        var backdropJson = await _localSettingsService.ReadSettingAsync("WindowBackdrop");
        if (backdropJson != null)
        {
            CurrentWindowBackdrop = (WindowBackdropType)Convert.ToInt32(backdropJson);
        }
        else
        {
            CurrentWindowBackdrop = WindowBackdropType.Acrylic;
        }

        var notifPosJson = await _localSettingsService.ReadSettingAsync("NotificationPosition");
        NotificationPosition = notifPosJson != null
            ? (NotificationPosition)Convert.ToInt32(notifPosJson)
            : NotificationPosition.BottomRight;

        var appThemeColorJson = await _localSettingsService.ReadSettingAsync("AppThemeColor");
        if (appThemeColorJson != null)
        {
            AppThemeColor = appThemeColorJson.ToString();
        }
        else
        {
            AppThemeColor = "";
        }

        var shortTermJson = await _localSettingsService.ReadSettingAsync("IsShortTermSupportEnabled");
        IsShortTermSupportEnabled = shortTermJson != null && Convert.ToBoolean(shortTermJson);

        var betterGIJson = await _localSettingsService.ReadSettingAsync("IsBetterGIIntegrationEnabled");
        IsBetterGIIntegrationEnabled = betterGIJson != null && Convert.ToBoolean(betterGIJson);

        var betterGICloseJson = await _localSettingsService.ReadSettingAsync("IsBetterGICloseOnExitEnabled");
        IsBetterGICloseOnExitEnabled = betterGICloseJson != null && Convert.ToBoolean(betterGICloseJson);

        var betterGIDelayJson = await _localSettingsService.ReadSettingAsync("BetterGIStartupDelaySeconds");
        BetterGIStartupDelaySeconds = betterGIDelayJson != null ? Math.Clamp(Convert.ToDouble(betterGIDelayJson), 0.0, 60.0) : 0.0;

        var soundJson = await _localSettingsService.ReadSettingAsync("IsStartupSoundEnabled");
        IsStartupSoundEnabled = soundJson != null && Convert.ToBoolean(soundJson);
        
        var autoCheckinJson = await _localSettingsService.ReadSettingAsync("IsAutoCheckinEnabled");
        IsAutoCheckinEnabled = autoCheckinJson != null && Convert.ToBoolean(autoCheckinJson);

        var cpuWarningEnabledJson = await _localSettingsService.ReadSettingAsync(ProcessCpuUsageMonitor.IsEnabledSettingKey);
        IsCpuUsageWarningEnabled = cpuWarningEnabledJson == null || Convert.ToBoolean(cpuWarningEnabledJson);

        var cpuWarningThresholdJson = await _localSettingsService.ReadSettingAsync(ProcessCpuUsageMonitor.ThresholdSettingKey);
        CpuUsageWarningThreshold = cpuWarningThresholdJson != null
            ? Math.Clamp(Convert.ToDouble(cpuWarningThresholdJson), 5.0, 100.0)
            : ProcessCpuUsageMonitor.DefaultCpuThreshold;

        var redeemNotifyJson = await _localSettingsService.ReadSettingAsync("IsRedeemCodeNotificationEnabled");
        IsRedeemCodeNotificationEnabled = redeemNotifyJson == null || Convert.ToBoolean(redeemNotifyJson);

        var usingHoyolabJson = await _localSettingsService.ReadSettingAsync("UsingHoyolabAccount");
        IsUsingHoyolabAccount = usingHoyolabJson != null && Convert.ToBoolean(usingHoyolabJson);

        var behaviorJson = await _localSettingsService.ReadSettingAsync("PostLaunchBehavior");
        PostLaunchBehavior postLaunchBehavior = PostLaunchBehavior.None;
        if (behaviorJson is string behaviorStr && Enum.TryParse<PostLaunchBehavior>(behaviorStr, out var parsed))
            postLaunchBehavior = parsed;
        _postLaunchBehavior = postLaunchBehavior;
        SelectedPostLaunchBehaviorItem = PostLaunchBehaviorItems.First(i => i.Value == postLaunchBehavior);
        
        var screenshotEnabledJson = await _localSettingsService.ReadSettingAsync("IsScreenshotEnabled");
        IsScreenshotEnabled = screenshotEnabledJson != null && Convert.ToBoolean(screenshotEnabledJson);

        var screenshotHotkeyJson = await _localSettingsService.ReadSettingAsync("ScreenshotHotkey");
        ScreenshotHotkey = screenshotHotkeyJson?.ToString() ?? "F12";

        var screenshotPathJson = await _localSettingsService.ReadSettingAsync("ScreenshotSavePath");
        ScreenshotSavePath = screenshotPathJson?.ToString();
        HasScreenshotSavePath = !string.IsNullOrEmpty(ScreenshotSavePath);

        var useThirdPartyCDNJson = await _localSettingsService.ReadSettingAsync("IsUseThirdPartyCDNEnabled");
        IsUseThirdPartyCDNEnabled = useThirdPartyCDNJson == null || Convert.ToBoolean(useThirdPartyCDNJson);

        var previewAnnouncementJson = await _localSettingsService.ReadSettingAsync("IsPreviewUpdateAnnouncementEnabled");
        IsPreviewUpdateAnnouncementEnabled = previewAnnouncementJson == null || Convert.ToBoolean(previewAnnouncementJson);

        var announcementViewModeJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.AnnouncementViewModeKey);
        if (announcementViewModeJson is string modeStr && Enum.TryParse<AnnouncementViewMode>(modeStr, out var parsedMode))
        {
            AnnouncementViewMode = parsedMode;
        }
        else
        {
            AnnouncementViewMode = AnnouncementViewMode.New;
        }

        var pluginMirrorJson = await _localSettingsService.ReadSettingAsync(PluginMirrorDownloadService.SettingKey);
        IsPluginMirrorAccelerationEnabled = pluginMirrorJson == null || Convert.ToBoolean(pluginMirrorJson);

        var customExeJson = await _localSettingsService.ReadSettingAsync(GameExeManager.CustomExeNameKey);
        CustomGameExeName = customExeJson?.ToString() ?? string.Empty;

        var soundPathJson = await _localSettingsService.ReadSettingAsync("StartupSoundPath");
        if (soundPathJson != null)
        {
            StartupSoundPath = soundPathJson.ToString();
            HasCustomStartupSound = File.Exists(StartupSoundPath);
        }
        else
        {
            StartupSoundPath = null;
            HasCustomStartupSound = false;
        }

        var overlayOpacityJson = await _localSettingsService.ReadSettingAsync("GlobalBackgroundOverlayOpacity");
        try
        {
            GlobalBackgroundOverlayOpacity = overlayOpacityJson != null ? Convert.ToDouble(overlayOpacityJson) : 0;
        }
        catch
        {
            GlobalBackgroundOverlayOpacity = 0;
        }

        var frameOpacityJson = await _localSettingsService.ReadSettingAsync("ContentFrameBackgroundOpacity");
        try
        {
            ContentFrameBackgroundOpacity = frameOpacityJson != null ? Convert.ToDouble(frameOpacityJson) : 0.5;
        }
        catch
        {
            ContentFrameBackgroundOpacity = 0.5;
        }
        
        var gameNewsCardColorJson = await _localSettingsService.ReadSettingAsync("GameNewsCardTextColor");
        GameNewsCardTextColor = gameNewsCardColorJson?.ToString() ?? "#FFFFFF";
        
        var gameNewsCardOpacityJson = await _localSettingsService.ReadSettingAsync("GameNewsCardTextOpacity");
        GameNewsCardTextOpacity = gameNewsCardOpacityJson != null ? Convert.ToDouble(gameNewsCardOpacityJson) : 1.0;

        var launchBtnColorJson = await _localSettingsService.ReadSettingAsync("LaunchButtonTextColor");
        LaunchButtonTextColor = launchBtnColorJson?.ToString() ?? "#FFFFFF";
        
        var launchBtnOpacityJson = await _localSettingsService.ReadSettingAsync("LaunchButtonTextOpacity");
        LaunchButtonTextOpacity = launchBtnOpacityJson != null ? Convert.ToDouble(launchBtnOpacityJson) : 1.0;

        var checkinColorJson = await _localSettingsService.ReadSettingAsync("GameCheckinTextColor");
        GameCheckinTextColor = checkinColorJson?.ToString() ?? "#FFFFFF";
        
        var checkinOpacityJson = await _localSettingsService.ReadSettingAsync("GameCheckinTextOpacity");
        GameCheckinTextOpacity = checkinOpacityJson != null ? Convert.ToDouble(checkinOpacityJson) : 1.0;

        var saveWindowSizeJson = await _localSettingsService.ReadSettingAsync("IsSaveWindowSizeEnabled");
        IsSaveWindowSizeEnabled = saveWindowSizeJson != null && Convert.ToBoolean(saveWindowSizeJson);

        var minSizeLimitJson = await _localSettingsService.ReadSettingAsync("IsMinWindowSizeLimitEnabled");
        IsMinWindowSizeLimitEnabled = minSizeLimitJson == null || Convert.ToBoolean(minSizeLimitJson);

        var hideNewsCardJson = await _localSettingsService.ReadSettingAsync("IsHideGameNewsCardEnabled");
        IsHideGameNewsCardEnabled = hideNewsCardJson != null && Convert.ToBoolean(hideNewsCardJson);

        var hideCheckinCardJson = await _localSettingsService.ReadSettingAsync("IsHideCheckinCardEnabled");
        IsHideCheckinCardEnabled = hideCheckinCardJson != null && Convert.ToBoolean(hideCheckinCardJson);

        var hideDailyNoteCardJson = await _localSettingsService.ReadSettingAsync("IsHideDailyNoteCardEnabled");
        IsHideDailyNoteCardEnabled = hideDailyNoteCardJson != null && Convert.ToBoolean(hideDailyNoteCardJson);

        _isUpdatingDailyNote = true;
        int activeCount = 0;

        var showResinJson = await _localSettingsService.ReadSettingAsync("ShowDailyNoteResin");
        ShowDailyNoteResin = showResinJson == null || Convert.ToBoolean(showResinJson);
        if (ShowDailyNoteResin) activeCount++;

        var showDailyTasksJson = await _localSettingsService.ReadSettingAsync("ShowDailyNoteDailyTasks");
        ShowDailyNoteDailyTasks = (showDailyTasksJson == null || Convert.ToBoolean(showDailyTasksJson)) && activeCount < 3;
        if (ShowDailyNoteDailyTasks) activeCount++;

        var showHomeCoinJson = await _localSettingsService.ReadSettingAsync("ShowDailyNoteHomeCoin");
        ShowDailyNoteHomeCoin = (showHomeCoinJson == null || Convert.ToBoolean(showHomeCoinJson)) && activeCount < 3;
        if (ShowDailyNoteHomeCoin) activeCount++;

        var showExpeditionsJson = await _localSettingsService.ReadSettingAsync("ShowDailyNoteExpeditions");
        ShowDailyNoteExpeditions = (showExpeditionsJson == null || Convert.ToBoolean(showExpeditionsJson)) && activeCount < 3;
        if (ShowDailyNoteExpeditions) activeCount++;

        var showTransformerJson = await _localSettingsService.ReadSettingAsync("ShowDailyNoteTransformer");
        ShowDailyNoteTransformer = (showTransformerJson == null || Convert.ToBoolean(showTransformerJson)) && activeCount < 3;
        
        var showPresetCardJson = await _localSettingsService.ReadSettingAsync("IsShowPresetCardEnabled");
        IsShowPresetCardEnabled = showPresetCardJson == null || Convert.ToBoolean(showPresetCardJson);

        _isUpdatingDailyNote = false;
        
        var showWidgetCardJson = await _localSettingsService.ReadSettingAsync("IsShowWidgetCardEnabled");
        IsShowWidgetCardEnabled = showWidgetCardJson == null || Convert.ToBoolean(showWidgetCardJson);

        var showWidgetGachaJson = await _localSettingsService.ReadSettingAsync("ShowWidgetGacha");
        ShowWidgetGacha = showWidgetGachaJson == null || Convert.ToBoolean(showWidgetGachaJson);
        
        var showWidgetAchievementJson = await _localSettingsService.ReadSettingAsync("ShowWidgetAchievement");
        ShowWidgetAchievement = showWidgetAchievementJson == null || Convert.ToBoolean(showWidgetAchievementJson);
        
        var showWidgetInventoryJson = await _localSettingsService.ReadSettingAsync("ShowWidgetInventory");
        ShowWidgetInventory = showWidgetInventoryJson == null || Convert.ToBoolean(showWidgetInventoryJson);
        
        var showWidgetPlayerRoleJson = await _localSettingsService.ReadSettingAsync("ShowWidgetPlayerRole");
        ShowWidgetPlayerRole = showWidgetPlayerRoleJson == null || Convert.ToBoolean(showWidgetPlayerRoleJson);
        
        var showWidgetDailyNoteWindowJson = await _localSettingsService.ReadSettingAsync("ShowWidgetDailyNoteWindow");
        ShowWidgetDailyNoteWindow = showWidgetDailyNoteWindowJson == null || Convert.ToBoolean(showWidgetDailyNoteWindowJson);
        
        var showWidgetVideoJson = await _localSettingsService.ReadSettingAsync("ShowWidgetVideo");
        ShowWidgetVideo = showWidgetVideoJson == null || Convert.ToBoolean(showWidgetVideoJson);
        
        var showWidgetBBSJson = await _localSettingsService.ReadSettingAsync("ShowWidgetBBS");
        ShowWidgetBBS = showWidgetBBSJson == null || Convert.ToBoolean(showWidgetBBSJson);

        var panelOpacityJson = await _localSettingsService.ReadSettingAsync("PanelBackgroundOpacity");
        try
        {
            PanelBackgroundOpacity = panelOpacityJson != null ? Convert.ToDouble(panelOpacityJson) : 0.5;
        }
        catch
        {
            PanelBackgroundOpacity = 0.5;
        }
        var bgImageOpacityJson = await _localSettingsService.ReadSettingAsync("GlobalBackgroundImageOpacity");
        try
        {
            GlobalBackgroundImageOpacity = bgImageOpacityJson != null ? Convert.ToDouble(bgImageOpacityJson) : 1.0;
        }
        catch
        {
            GlobalBackgroundImageOpacity = 1.0;
        }

        var gameCheckinJson = await _localSettingsService.ReadSettingAsync("IsGameCheckinEnabled");
        IsGameCheckinEnabled = gameCheckinJson == null || Convert.ToBoolean(gameCheckinJson);

        var communityCheckinJson = await _localSettingsService.ReadSettingAsync("IsCommunityCheckinEnabled");
        IsCommunityCheckinEnabled = communityCheckinJson == null || Convert.ToBoolean(communityCheckinJson);

        var communityLikeJson = await _localSettingsService.ReadSettingAsync("IsCommunityLikeEnabled");
        IsCommunityLikeEnabled = communityLikeJson != null && Convert.ToBoolean(communityLikeJson);

        var communityReadJson = await _localSettingsService.ReadSettingAsync("IsCommunityReadEnabled");
        IsCommunityReadEnabled = communityReadJson != null && Convert.ToBoolean(communityReadJson);

        var communityShareJson = await _localSettingsService.ReadSettingAsync("IsCommunityShareEnabled");
        IsCommunityShareEnabled = communityShareJson != null && Convert.ToBoolean(communityShareJson);

        var cloudGameCheckinJson = await _localSettingsService.ReadSettingAsync("IsCloudGameCheckinEnabled");
        IsCloudGameCheckinEnabled = cloudGameCheckinJson != null && Convert.ToBoolean(cloudGameCheckinJson);

        var batchCheckinJson = await _localSettingsService.ReadSettingAsync("IsBatchCheckinEnabled");
        IsBatchCheckinEnabled = batchCheckinJson != null && Convert.ToBoolean(batchCheckinJson);
        
        var priorityJson = await _localSettingsService.ReadSettingAsync("AppProcessPriority");
        if (priorityJson != null)
        {
            AppProcessPriority = (AppProcessPriority)Convert.ToInt32(priorityJson);
        }
        else
        {
            AppProcessPriority = AppProcessPriority.Normal;
        }
        ApplyProcessPriority(AppProcessPriority);
        
        var captchaPopupJson = await _localSettingsService.ReadSettingAsync("IsCaptchaPopupDisabled");
        IsCaptchaPopupDisabled = captchaPopupJson != null && Convert.ToBoolean(captchaPopupJson);
    }

    #endregion
}
