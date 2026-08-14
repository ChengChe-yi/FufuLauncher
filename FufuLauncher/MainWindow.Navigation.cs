/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Numerics;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Models;
using FufuLauncher.Services;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;

namespace FufuLauncher;

public sealed partial class MainWindow
{
    #region Navigation & Layout

    private async void NavigationView_Loaded(object sender, RoutedEventArgs e)
    {
        bool isAccepted = false;
        try
        {
            var accepted = await _localSettingsService.ReadSettingAsync("UserAgreementAccepted");
            isAccepted = accepted != null && Convert.ToBoolean(accepted);
        }
        catch { }

        if (!isAccepted)
        {
            return;
        }

        await PerformMainInitAsync();
    }

    private async Task PerformMainInitAsync()
    {
        try
        {
            foreach (var item in NavigationView.MenuItems)
            {
                if (item is FrameworkElement uiItem) SetupSpringAnimation(uiItem);
            }
            foreach (var item in NavigationView.FooterMenuItems)
            {
                if (item is FrameworkElement uiItem) SetupSpringAnimation(uiItem);
            }

            await LoadFrameBackgroundOpacityAsync();
            await LoadOverlayOpacityAsync();
            await LoadAcrylicOverlaySettingAsync();
            await LoadPageOverlayOpacitySettingAsync();
            await LoadHamburgerButtonSettingAsync();
            await LoadAndApplyAcrylicSettingAsync();
            await LoadGlobalBackgroundAsync();
            await LoadMinimizeToTraySettingAsync();
            await LoadMinWindowSizeLimitSettingAsync();
            await LoadNotificationPositionAsync();
            await LoadNavItemVisibilityAsync();
            ShowMainContent();

            _ = Task.Run(async () =>
            {
                var starService = new StarPromotionService(_localSettingsService, dispatcherQueue);
                await starService.CheckAndShowPromptAsync();
            });

            _ = Task.Run(async () =>
            {
                try
                {
                    var accountManager = App.GetService<AccountManager>();
                    var activeId = accountManager.ActiveAccountId;
                    if (activeId != null)
                    {
                        var cookies = await accountManager.LoadCookiesAsync(activeId);
                        if (cookies != null && cookies.Count > 0)
                        {
                            var refreshService = new TokenRefreshService();
                            var newCookies = await refreshService.RefreshCookieAsync(cookies);
                            if (newCookies != null)
                            {
                                await accountManager.UpdateCookiesAsync(activeId, newCookies);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"启动时刷新 Token 失败: {ex.Message}");
                }
            });
            _ = Task.Run(async () => await HashValidationService.ValidateFilesAsync());
        }
        catch { ShowMainContent(); }
    }

    private void SetupSpringAnimation(FrameworkElement element)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        element.SizeChanged += (_, e) =>
        {
            visual.CenterPoint = new Vector3((float)e.NewSize.Width / 2f, (float)e.NewSize.Height / 2f, 0f);
        };

        element.PointerPressed += (_, _) =>
        {
            var anim = compositor.CreateSpringVector3Animation();
            anim.Target = "Scale";
            anim.FinalValue = new Vector3(0.92f, 0.92f, 1f);

            anim.Period = TimeSpan.FromMilliseconds(20);
            anim.DampingRatio = 0.6f;

            visual.StartAnimation("Scale", anim);
        };

        void ResetScale()
        {
            var anim = compositor.CreateSpringVector3Animation();
            anim.Target = "Scale";
            anim.FinalValue = new Vector3(1f, 1f, 1f);

            anim.Period = TimeSpan.FromMilliseconds(60);
            anim.DampingRatio = 0.5f;

            visual.StartAnimation("Scale", anim);
        }

        element.PointerReleased += (_, _) => ResetScale();
        element.PointerExited += (_, _) => ResetScale();
    }

    private void ShowAgreementPage()
    {
        _isMainUiLoaded = false;
        SystemMessageBar.Visibility = Visibility.Collapsed;
        _networkMonitorService.Stop();

        Width = 850;
        Height = 560;
        WindowManagerHelper.CenterWindowOnScreen(AppWindow, Width, Height);

        AgreementFrame.Visibility = Visibility.Visible;
        NavigationView.Visibility = Visibility.Collapsed;
        AgreementFrame.Navigate(typeof(Views.AgreementPage));
        SyncPageTheme();
    }

    private async void ShowMainContent()
    {
        AgreementFrame.Visibility = Visibility.Collapsed;
        NavigationView.Visibility = Visibility.Visible;
        NavigationView.SelectedItem = NavigationView.MenuItems[0];

        try
        {
            if (ContentFrame.CurrentSourcePageType != typeof(Views.MainPage))
                ContentFrame.Navigate(typeof(Views.MainPage));
        }
        catch (InvalidCastException ex)
        {
            Debug.WriteLine($"MainPage 导航类型转换异常: {ex.Message}");
        }

        SyncPageTheme();

        UpdatePageOverlayState(true);

        _isMainUiLoaded = true;
        SystemMessageBar.Visibility = Visibility.Visible;
        _networkMonitorService.Start();
        _ = _networkMonitorService.CheckNetworkAndProxyStatusAsync();

        var notifyEnabledObj = await _localSettingsService.ReadSettingAsync("IsRedeemCodeNotificationEnabled");
        if (notifyEnabledObj == null || Convert.ToBoolean(notifyEnabledObj))
        {
            var redeemService = new RedeemCodeReminderService(_localSettingsService);
            _ = redeemService.CheckRedeemCodesAsync(msg =>
            {
                dispatcherQueue.TryEnqueue(() => ShowNotification(msg));
            });
        }
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem selectedItem)
        {
            var viewModelTag = selectedItem.Tag?.ToString();

            if (viewModelTag == "FufuLauncher.ViewModels.SettingsViewModel")
            {
                var anim = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = new Duration(TimeSpan.FromSeconds(0.7)),
                    EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
                };

                Storyboard.SetTarget(anim, SettingsIconRotation);
                Storyboard.SetTargetProperty(anim, "Angle");

                var sb = new Storyboard();
                sb.Children.Add(anim);
                sb.Begin();
            }

            if (!string.IsNullOrEmpty(viewModelTag)) NavigateToPage(viewModelTag);
        }
    }

    private void NavigationView_PaneOpened(NavigationView sender, object args)
    {
        if (_notificationPosition == NotificationPosition.TopLeft || _notificationPosition == NotificationPosition.BottomLeft)
        {
            ApplyNotificationPosition(_notificationPosition);
        }
    }

    private void NavigationView_PaneClosed(NavigationView sender, object args)
    {
        if (_notificationPosition == NotificationPosition.TopLeft || _notificationPosition == NotificationPosition.BottomLeft)
        {
            ApplyNotificationPosition(_notificationPosition);
        }
    }

    internal async void NavigateToPage(string viewModelTag)
    {

        if (!IsNavItemVisible(viewModelTag))
        {
            ShowNotification(new NotificationMessage("HiddenFeatureTitle".GetLocalized(), "HiddenFeatureMessage".GetLocalized(), NotificationType.Warning, 3000));
            NavigationView.SelectedItem = null;
            ContentFrame.Navigate(typeof(Views.HiddenPage), null, new SuppressNavigationTransitionInfo());
            SyncPageTheme();
            return;
        }

        var pageType = viewModelTag switch
        {
            "FufuLauncher.ViewModels.MainViewModel" => typeof(Views.MainPage),
            "FufuLauncher.ViewModels.BlankViewModel" => typeof(Views.BlankPage),
            "FufuLauncher.ViewModels.SettingsViewModel" => typeof(Views.SettingsPage),
            "FufuLauncher.ViewModels.AccountViewModel" => typeof(Views.AccountPage),
            "FufuLauncher.ViewModels.OtherViewModel" => typeof(Views.OtherPage),
            "FufuLauncher.ViewModels.CalculatorViewModel" => typeof(Views.CalculatorPage),
            "FufuLauncher.ViewModels.ControlPanelModel" => typeof(Views.PanelPage),
            "FufuLauncher.ViewModels.PluginViewModel" => typeof(Views.PluginPage),
            "FufuLauncher.ViewModels.PluginStoreViewModel" => typeof(Views.PluginStorePage),
            "FufuLauncher.ViewModels.DataViewModel" => typeof(Views.DataPage),
            "FufuLauncher.ViewModels.PluginSettingsViewModel" => typeof(Views.PluginSettingsPage),
            "FufuLauncher.ViewModels.BackpackViewModel" => typeof(Views.BackpackPage),
            "FufuLauncher.ViewModels.HelpViewModel" => typeof(Views.HelpPage),
            "FufuLauncher.ViewModels.CommunityViewModel" => typeof(Views.CommunityPage),
            _ => null
        };

        if (pageType != null && ContentFrame.CurrentSourcePageType != pageType)
        {
            if (ContentFrame.Content is Page currentPage)
            {
                var exitStoryboard = currentPage.FindName("ExitStoryboard") as Storyboard;
                if (exitStoryboard != null)
                {
                    exitStoryboard.Begin();
                    await Task.Delay(300);
                }
            }

            try
            {
                ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());
                SyncPageTheme();
            }
            catch (InvalidCastException ex)
            {
                Debug.WriteLine($"页面导航类型转换异常: {ex.Message}");
                return;
            }

            var isMainPage = pageType == typeof(Views.MainPage);
            UpdatePageOverlayState(isMainPage);

            if (isMainPage)
            {
                var mainItem = GetAllNavItems().FirstOrDefault(i => i.Tag?.ToString() == "FufuLauncher.ViewModels.MainViewModel");
                if (mainItem != null)
                    NavigationView.SelectedItem = mainItem;
            }
        }
    }

    public async Task NavigateToSettingsUpdateSectionAsync()
    {
        if (_isExit) return;
        try { Activate(); } catch (System.Runtime.InteropServices.COMException) { return; }

        for (var i = 0; i < 40 && !_isMainUiLoaded; i++)
        {
            await Task.Delay(100);
        }

        var settingsItem = NavigationView.FooterMenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == "FufuLauncher.ViewModels.SettingsViewModel");

        if (settingsItem != null)
        {
            NavigationView.SelectedItem = settingsItem;
        }
        else
        {
            NavigateToPage("FufuLauncher.ViewModels.SettingsViewModel");
        }

        for (var i = 0; i < 40; i++)
        {
            if (ContentFrame.Content is Views.SettingsPage settingsPage)
            {
                await settingsPage.NavigateToUpdateSectionAsync();
                return;
            }

            await Task.Delay(100);
        }
    }

    public async Task NavigateToSettingsPageAsync()
    {
        if (_isExit) return;
        try { Activate(); } catch (System.Runtime.InteropServices.COMException) { return; }

        for (var i = 0; i < 40 && !_isMainUiLoaded; i++)
        {
            await Task.Delay(100);
        }

        var settingsItem = NavigationView.FooterMenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == "FufuLauncher.ViewModels.SettingsViewModel");

        if (settingsItem != null)
        {
            NavigationView.SelectedItem = settingsItem;
        }
        else
        {
            NavigateToPage("FufuLauncher.ViewModels.SettingsViewModel");
        }

        for (var i = 0; i < 40; i++)
        {
            if (ContentFrame.Content is Views.SettingsPage settingsPage)
            {
                await settingsPage.NavigateToCheckinSettingsAsync();
                return;
            }

            await Task.Delay(100);
        }
    }

    public async Task NavigateToAccountPageAsync()
    {
        if (_isExit) return;
        try { Activate(); } catch (System.Runtime.InteropServices.COMException) { return; }

        for (var i = 0; i < 40 && !_isMainUiLoaded; i++)
        {
            await Task.Delay(100);
        }

        var accountItem = NavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == "FufuLauncher.ViewModels.AccountViewModel");

        if (accountItem != null)
            NavigationView.SelectedItem = accountItem;
        else
            NavigateToPage("FufuLauncher.ViewModels.AccountViewModel");
    }

    public async Task LoadNavItemVisibilityAsync()
    {
        var settings = App.GetService<ILocalSettingsService>();
        var allKeys = new[]
        {
            "MainViewModel", "PluginSettingsViewModel", "ControlPanelModel",
            "BlankViewModel", "AccountViewModel", "OtherViewModel",
            "PluginViewModel", "DataViewModel", "BackpackViewModel", "HelpViewModel",
            "CommunityViewModel", "CalculatorViewModel", "SettingsViewModel"
        };

        foreach (var key in allKeys)
        {
            var val = await settings.ReadSettingAsync($"NavVisible_{key}");
            bool visible = val is bool b ? b : (val is string s && bool.TryParse(s, out var p) ? p : true);
            _navItemVisibility[$"FufuLauncher.ViewModels.{key}"] = visible;
        }


        foreach (var item in GetAllNavItems())
        {
            var tag = item.Tag?.ToString();
            if (tag != null && _navItemVisibility.TryGetValue(tag, out var visible))
            {
                item.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    private bool IsNavItemVisible(string viewModelKey)
    {
        return _navItemVisibility.TryGetValue(viewModelKey, out var visible) ? visible : true;
    }

    private void ApplyNavItemVisibility(NavItemConfig config)
    {
        _navItemVisibility[config.ViewModelKey] = config.IsUserVisible;

        foreach (var item in GetAllNavItems())
        {
            if (item.Tag?.ToString() == config.ViewModelKey)
            {
                item.Visibility = config.IsUserVisible ? Visibility.Visible : Visibility.Collapsed;
                break;
            }
        }
    }

    private IEnumerable<NavigationViewItem> GetAllNavItems()
    {
        return NavigationView.MenuItems
            .Concat(NavigationView.FooterMenuItems)
            .OfType<NavigationViewItem>();
    }

    private void UpdatePageOverlayState(bool isMainPage)
    {
        try
        {
            var screenHeight = Bounds.Height > 0 ? Bounds.Height : 1000;
            var targetOpacity = _isPageOverlaySemiTransparent ? _pageOverlayTargetOpacity : 1.0;

            if (isMainPage && _isOverlayShown)
            {
                var translateAnim = new DoubleAnimation
                {
                    From = 0,
                    To = screenHeight + 50,
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var opacityAnim = new DoubleAnimation
                {
                    From = targetOpacity,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(400),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
                };

                var sb = new Storyboard();
                Storyboard.SetTarget(translateAnim, OverlayTranslate);
                Storyboard.SetTargetProperty(translateAnim, "Y");

                Storyboard.SetTarget(opacityAnim, PageBackgroundOverlay);
                Storyboard.SetTargetProperty(opacityAnim, "Opacity");

                sb.Children.Add(translateAnim);
                sb.Children.Add(opacityAnim);
                sb.Begin();

                _isOverlayShown = false;
            }
            else if (!isMainPage && !_isOverlayShown)
            {
                OverlayTranslate.Y = screenHeight;
                PageBackgroundOverlay.Opacity = 0.0;

                var translateAnim = new DoubleAnimation
                {
                    From = screenHeight,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var opacityAnim = new DoubleAnimation
                {
                    From = 0.0,
                    To = targetOpacity,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                var sb = new Storyboard();
                Storyboard.SetTarget(translateAnim, OverlayTranslate);
                Storyboard.SetTargetProperty(translateAnim, "Y");

                Storyboard.SetTarget(opacityAnim, PageBackgroundOverlay);
                Storyboard.SetTargetProperty(opacityAnim, "Opacity");

                sb.Children.Add(translateAnim);
                sb.Children.Add(opacityAnim);
                sb.Begin();

                _isOverlayShown = true;
            }
            else if (!isMainPage && _isOverlayShown)
            {
                OverlayTranslate.Y = 0;
                PageBackgroundOverlay.Opacity = targetOpacity;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[MainWindow] 遮罩动画异常: {ex.Message}");
        }
    }

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        if (!_isOverlayShown)
        {
            OverlayTranslate.Y = Bounds.Height + 100;
        }
    }

    private async Task LoadHamburgerButtonSettingAsync()
    {
        try
        {
            var valueObj = await _localSettingsService.ReadSettingAsync("IsHamburgerButtonEnabled");
            _isHamburgerButtonEnabled = valueObj != null && Convert.ToBoolean(valueObj);
            ApplyHamburgerButtonVisibility(_isHamburgerButtonEnabled);
        }
        catch { _isHamburgerButtonEnabled = false; }
    }

    private void ApplyHamburgerButtonVisibility(bool isEnabled)
    {
        NavigationView.IsPaneToggleButtonVisible = isEnabled;
        if (!isEnabled)
        {
            NavigationView.IsPaneOpen = false;
        }
    }

    #endregion
}
