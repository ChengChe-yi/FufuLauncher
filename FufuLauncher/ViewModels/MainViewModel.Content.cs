/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Models;
using FufuLauncher.Services;
using Microsoft.UI.Dispatching;

namespace FufuLauncher.ViewModels;

public partial class MainViewModel
{
    #region 资讯内容与轮播
    [ObservableProperty] private ObservableCollection<BannerItem> _banners = new();
    [ObservableProperty] private ObservableCollection<PostItem> _activityPosts = new();
    [ObservableProperty] private ObservableCollection<PostItem> _announcementPosts = new();
    [ObservableProperty] private ObservableCollection<PostItem> _infoPosts = new();
    [ObservableProperty] private ObservableCollection<SocialMediaItem> _socialMediaList = new();

    private DispatcherQueueTimer _bannerTimer;

    private BannerItem _currentBanner;
    public string CurrentDayText => DateTime.Now.Day.ToString();
    public BannerItem CurrentBanner
    {
        get => _currentBanner;
        set
        {
            SetProperty(ref _currentBanner, value);
        }
    }

    private async Task LoadContentAsync()
    {
        if (Banners != null && Banners.Count > 0)
        {
            if (CurrentBanner == null)
            {
                CurrentBanner = Banners[0];
            }

            _bannerTimer?.Start();

            return;
        }

        try
        {
            var serverJson = await _localSettingsService.ReadSettingAsync(LocalSettingsService.BackgroundServerKey);
            int serverValue = serverJson != null ? Convert.ToInt32(serverJson) : 0;
            var server = (Models.ServerType)serverValue;

            var content = await _contentService.GetGameContentAsync(server);

            if (content != null)
            {
                await UpdateUI(() =>
                {
                    _bannerTimer?.Stop();
                    CurrentBanner = null;

                    Banners.Clear();
                    foreach (var banner in content.Banners ?? Array.Empty<BannerItem>())
                    {
                        Banners.Add(banner);
                    }

                    var posts = content.Posts ?? Array.Empty<PostItem>();

                    ActivityPosts.Clear();
                    foreach (var post in posts.Where(p => p.Type == "POST_TYPE_ACTIVITY"))
                        ActivityPosts.Add(post);

                    AnnouncementPosts.Clear();
                    foreach (var post in posts.Where(p => p.Type == "POST_TYPE_ANNOUNCE"))
                        AnnouncementPosts.Add(post);

                    InfoPosts.Clear();
                    foreach (var post in posts.Where(p => p.Type == "POST_TYPE_INFO"))
                        InfoPosts.Add(post);

                    SocialMediaList.Clear();
                    foreach (var item in content.SocialMediaList ?? Array.Empty<SocialMediaItem>())
                    {
                        SocialMediaList.Add(item);
                    }

                    if (Banners.Count > 0)
                    {
                        _dispatcherQueue.TryEnqueue(async () =>
                        {
                            try
                            {
                                await Task.Delay(50);

                                if (Banners.Count > 0)
                                {
                                    CurrentBanner = Banners[0];
                                    _bannerTimer?.Start();
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"设置 Banner 选中项失败: {ex.Message}");
                            }
                        });
                    }
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"内容加载失败: {ex.Message}");
        }
    }

    private void RotateBanner()
    {
        if (Banners == null || Banners.Count < 2) return;

        if (CurrentBanner == null)
        {
            CurrentBanner = Banners[0];
            return;
        }

        try
        {
            var currentIndex = Banners.IndexOf(CurrentBanner);
            if (currentIndex == -1) currentIndex = 0;

            var nextIndex = (currentIndex + 1) % Banners.Count;
            CurrentBanner = Banners[nextIndex];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"轮播图切换错误: {ex.Message}");
        }
    }
    #endregion
}
