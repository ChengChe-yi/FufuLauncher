/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;

namespace FufuLauncher.Views;

public sealed partial class AchievementWindow
{
    private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.SearchText) ||
            e.PropertyName == nameof(ViewModel.HideCompleted) ||
            e.PropertyName == nameof(ViewModel.SelectedVersion))
        {
            ApplyFilters();
        }

        if (e.PropertyName == nameof(AchievementViewModel.IsCategoryGridMode))
        {
            if (ViewModel.IsCategoryGridMode)
            {
                PlayEntranceAnimation(CategoryGridView);
            }
            else
            {
                PlayEntranceAnimation(DetailView);
            }
        }
    }

    private void PlayEntranceAnimation(UIElement target)
    {
        Storyboard sb = new();

        DoubleAnimation translateAnim = new()
        {
            From = 30,
            To = 0,
            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        Storyboard.SetTarget(translateAnim, target.RenderTransform);
        Storyboard.SetTargetProperty(translateAnim, "Y");

        DoubleAnimation opacityAnim = new()
        {
            From = 0,
            To = 1,
            Duration = new Duration(TimeSpan.FromMilliseconds(300))
        };
        Storyboard.SetTarget(opacityAnim, target);
        Storyboard.SetTargetProperty(opacityAnim, "Opacity");

        sb.Children.Add(translateAnim);
        sb.Children.Add(opacityAnim);
        sb.Begin();
    }

    private void ApplyFilters()
    {
        string search = ViewModel.SearchText?.Trim().ToLower();
        bool isGlobalSearch = !string.IsNullOrEmpty(search);

        IEnumerable<AchievementItem> sourceList;

        if (isGlobalSearch)
        {
            sourceList = ViewModel.Categories.SelectMany(c => c.Achievements);
        }
        else
        {
            if (ViewModel.SelectedCategory == null)
            {
                ViewModel.FilteredAchievements.Clear();
                return;
            }
            sourceList = ViewModel.SelectedCategory.Achievements;
        }

        var resultList = new List<AchievementItem>();
        bool isFilterVer = ViewModel.SelectedVersion != "AchievementWindow_AllVersions".GetLocalized() && !string.IsNullOrEmpty(ViewModel.SelectedVersion);

        foreach (var item in sourceList)
        {
            if (item.IsGroup)
            {
                bool matchGroup = false;

                if (isGlobalSearch)
                {
                    if (item.Title != null && item.Title.ToLower().Contains(search)) matchGroup = true;
                    else if (item.Children.Any(c => c.Description != null && c.Description.ToLower().Contains(search))) matchGroup = true;
                }
                else
                {
                    matchGroup = true;
                }

                if (isFilterVer)
                {
                    if (item.Version != ViewModel.SelectedVersion && !item.Children.Any(c => c.Version == ViewModel.SelectedVersion))
                        matchGroup = false;
                }

                if (ViewModel.HideCompleted)
                {
                    if (item.Children.All(c => c.IsCompleted)) matchGroup = false;
                }

                if (matchGroup) resultList.Add(item);
            }
            else
            {
                bool match = true;

                if (isGlobalSearch)
                {
                    if (!((item.Title != null && item.Title.ToLower().Contains(search)) ||
                          (item.Description != null && item.Description.ToLower().Contains(search))))
                        match = false;
                }

                if (ViewModel.HideCompleted && item.IsCompleted) match = false;

                if (isFilterVer && item.Version != ViewModel.SelectedVersion) match = false;

                if (match) resultList.Add(item);
            }
        }

        ViewModel.FilteredAchievements.Clear();
        foreach (var item in resultList) ViewModel.FilteredAchievements.Add(item);
    }

    private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e) => ApplyFilters();
    private void OnToggleViewMode(object sender, RoutedEventArgs e) => ViewModel.IsCategoryGridMode = !ViewModel.IsCategoryGridMode;

    private void OnCategoryGridItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is AchievementCategory cat)
        {
            ViewModel.SelectedCategory = cat;
            ViewModel.IsCategoryGridMode = false;
            ApplyFilters();
        }
    }

    private void OnSearchGuideClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is AchievementItem item)
        {
            try
            {
                string keyword = WebUtility.UrlEncode(item.Title);
                string url = $"https://www.miyoushe.com/ys/search?keyword={keyword}";

                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ViewModel.StatusMessage = "AchievementWindow_CannotOpenBrowser".GetLocalized() + ": " + ex.Message;
            }
        }
    }
}
