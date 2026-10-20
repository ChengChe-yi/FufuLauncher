/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.ComponentModel;
using FufuLauncher.Models.CycleChallenge;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace FufuLauncher.Views;

/// <summary>
/// 周期挑战主页面：页头（账号 / 角色）、三模式切换、期次选择与内容宿主。
/// 内容由三个独立面板控件各自解析单期 JSON 并渲染。
/// </summary>
public sealed partial class CycleChallengePage : Page
{
    private bool _subscribed;

    public CycleChallengePage()
    {
        ViewModel = App.GetService<CycleChallengeViewModel>();
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public CycleChallengeViewModel ViewModel { get; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_subscribed)
        {
            return;
        }

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
        _subscribed = true;
        UpdateContent();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (!_subscribed)
        {
            return;
        }

        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _subscribed = false;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        await ViewModel.InitializeAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CycleChallengeViewModel.Payload)
            or nameof(CycleChallengeViewModel.CurrentMode)
            or nameof(CycleChallengeViewModel.HasData)
            or nameof(CycleChallengeViewModel.HasError))
        {
            DispatcherQueue.TryEnqueue(UpdateContent);
        }
    }

    private async void OnAbyssChipClick(object sender, RoutedEventArgs e) =>
        await ViewModel.SetModeAsync(ChallengeMode.SpiralAbyss);

    private async void OnTheaterChipClick(object sender, RoutedEventArgs e) =>
        await ViewModel.SetModeAsync(ChallengeMode.Theater);

    private async void OnStygianChipClick(object sender, RoutedEventArgs e) =>
        await ViewModel.SetModeAsync(ChallengeMode.Stygian);

    /// <summary>按当前模式切换面板可见性，并把单期 JSON 交给对应面板渲染。</summary>
    private void UpdateContent()
    {
        var mode = ViewModel.CurrentMode;

        AbyssHost.Visibility = mode == ChallengeMode.SpiralAbyss ? Visibility.Visible : Visibility.Collapsed;
        TheaterHost.Visibility = mode == ChallengeMode.Theater ? Visibility.Visible : Visibility.Collapsed;
        StygianHost.Visibility = mode == ChallengeMode.Stygian ? Visibility.Visible : Visibility.Collapsed;

        AbyssChip.IsChecked = mode == ChallengeMode.SpiralAbyss;
        TheaterChip.IsChecked = mode == ChallengeMode.Theater;
        StygianChip.IsChecked = mode == ChallengeMode.Stygian;

        var payload = ViewModel.Payload;
        var hasPayload = !string.IsNullOrEmpty(payload) && ViewModel.HasData && !ViewModel.HasError;
        EmptyText.Visibility = hasPayload ? Visibility.Collapsed : Visibility.Visible;

        if (!hasPayload)
        {
            // 无数据时折叠所有面板，避免旧账号/旧模式的内容残留在空态下面
            AbyssHost.Visibility = Visibility.Collapsed;
            TheaterHost.Visibility = Visibility.Collapsed;
            StygianHost.Visibility = Visibility.Collapsed;
            return;
        }

        // 时间显示时区：cn_* → UTC+8，其它 → UTC（面板按区服换算）
        var region = ViewModel.CurrentRegion;
        AbyssPanel.Region = region;
        TheaterPanel.Region = region;
        StygianPanel.Region = region;

        switch (mode)
        {
            case ChallengeMode.SpiralAbyss:
                AbyssPanel.Show(payload!);
                break;
            case ChallengeMode.Theater:
                TheaterPanel.Show(payload!);
                break;
            case ChallengeMode.Stygian:
                StygianPanel.Show(payload!);
                break;
        }
    }
}
