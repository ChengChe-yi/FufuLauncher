/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Models.GameAnnouncement;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;

namespace FufuLauncher.Views;

public sealed partial class GameAnnouncementListPage : Page
{
    public GameAnnouncementViewModel ViewModel
    {
        get;
    }

    public GameAnnouncementListPage()
    {
        ViewModel = App.GetService<GameAnnouncementViewModel>();
        InitializeComponent();
    }

    protected async override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        try
        {
            await ViewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GameAnnouncementListPage] 初始化公告失败: {ex.Message}");
        }
    }

    private void AnnouncementCard_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is GameAnnouncement announcement)
        {
            Frame.Navigate(typeof(GameAnnouncementContentPage), announcement);
        }
    }

    private void AnnouncementCard_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        ZoomCardImage((FrameworkElement)sender, 1.06);
    }

    private void AnnouncementCard_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        ZoomCardImage((FrameworkElement)sender, 1.0);
    }
    
    private static void ZoomCardImage(FrameworkElement card, double target)
    {
        if (card.FindName("BannerImage") is GameAnnouncementBanner banner)
        {
            banner.AnimateZoom(target);
        }
    }

    private void ClassicMode_Click(object sender, RoutedEventArgs e)
    {
        new AnnouncementWindow().Activate();
    }
}
