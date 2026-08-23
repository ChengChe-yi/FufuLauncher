/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.ComponentModel;
using System.Diagnostics;
using FufuLauncher.Contracts.Services;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace FufuLauncher.Views;

// MainPage 主文件：仅保留构造、属性与页面生命周期；功能实现按区域拆分至同目录 MainPage.*.cs 分部文件。
public sealed partial class MainPage : Page
{
    private bool _isInitialized;

    public MainViewModel ViewModel
    {
        get;
    }
    public XamlUICommand OpenLinkCommand
    {
        get;
    }

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        DataContext = ViewModel;
        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        Loaded += (_, _) =>
        {
            LaunchButtonOverlayBorder.Opacity = ViewModel.IsGameRunning ? 0.0 : 1.0;
        };

        ViewModel.InfoCardToggledRequested += OnInfoCardToggledRequested;

        OpenLinkCommand = new XamlUICommand();
        OpenLinkCommand.ExecuteRequested += (sender, args) =>
        {
            if (args.Parameter is string url)
            {
                OpenLink(url);
            }
        };
    }

    #region 页面生命周期

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (_isInitialized)
        {
            _ = ViewModel.OnPageReturnedAsync();
        }
    }

    private async void Page_Loaded(object sender, RoutedEventArgs e)
    {
        EntranceStoryboard.Begin();

        InitializeBannerDisplay();

        SyncDailyNoteState();

        InitializeNewsSkeleton();

        if (!_isInitialized)
        {
            var localSettings = App.GetService<ILocalSettingsService>();
            var accepted = await localSettings.ReadSettingAsync("UserAgreementAccepted");
            if (accepted == null || !Convert.ToBoolean(accepted)) return;
            await ViewModel.InitializeAsync();
            _isInitialized = true;
        }
    }

    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsGameRunning))
        {
            AnimateLaunchButtonOverlay(ViewModel.IsGameRunning ? 0.0 : 1.0);
        }
        else if (e.PropertyName == nameof(MainViewModel.CurrentBanner))
        {
            _ = DispatcherQueue.TryEnqueue(() => TransitionToBanner(ViewModel.CurrentBanner));
        }
        else if (e.PropertyName == nameof(MainViewModel.IsDailyNoteLoaded))
        {
            _ = DispatcherQueue.TryEnqueue(() => AnimateDailyNoteTransition(ViewModel.IsDailyNoteLoaded));
        }
        else if (e.PropertyName == nameof(MainViewModel.IsNewsLoaded))
        {
            _ = DispatcherQueue.TryEnqueue(OnNewsContentLoaded);
        }
    }

    #endregion

    #region 共享辅助

    private async void OpenLink(string url)
    {
        if (!string.IsNullOrEmpty(url))
        {
            try
            {
                var uri = new Uri(url);
                await Windows.System.Launcher.LaunchUriAsync(uri);
                Debug.WriteLine($"打开链接: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"打开链接失败: {ex.Message}");
            }
        }
    }

    private static DoubleAnimation CreateDoubleAnimation(DependencyObject target, string property, double to, Duration duration, EasingFunctionBase easing)
    {
        var animation = new DoubleAnimation
        {
            To = to,
            Duration = duration,
            EasingFunction = easing,
            EnableDependentAnimation = true
        };

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        return animation;
    }

    #endregion
}
