/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    #region 导航与滚动联动

    private void SettingsNavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (_isNavigatingFromMenu) return;

        if (args.SelectedItem is NavigationViewItem selectedItem &&
            selectedItem.Tag is string tag)
        {
            _isNavigatingFromMenu = true;

            // Safety net: clear lock if ViewChanged never fires
            // (happens when element is already visible but can't scroll to top)
            _navLockTimer?.Stop();
            _navLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            _navLockTimer.Tick += (s, e) =>
            {
                ((DispatcherTimer)s).Stop();
                _isNavigatingFromMenu = false;
            };
            _navLockTimer.Start();

            var element = FindName(tag) as FrameworkElement;
            if (element != null)
            {
                if (element.ActualHeight > 0)
                {
                    BringElementIntoView(element);
                }
                else
                {
                    RoutedEventHandler loadedHandler = null;
                    loadedHandler = (s, e) =>
                    {
                        BringElementIntoView(element);
                        element.Loaded -= loadedHandler;
                    };
                    element.Loaded += loadedHandler;
                }
            }
        }
    }

    private void SettingsScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
    {
        if (e.IsIntermediate) return;

        // Scroll from nav click completed — release lock, skip sync
        if (_isNavigatingFromMenu)
        {
            _navLockTimer?.Stop();
            _isNavigatingFromMenu = false;
            return;
        }

        var scrollViewer = (ScrollViewer)sender;
        double anchor = scrollViewer.Padding.Top + 1;
        var visibleTag = (string?)null;

        foreach (var tag in _sectionTags)
        {
            var element = FindName(tag) as FrameworkElement;
            if (element == null) continue;

            var transform = element.TransformToVisual(scrollViewer);
            var position = transform.TransformPoint(new Windows.Foundation.Point(0, 0));

            if (position.Y <= anchor)
            {
                visibleTag = tag;
            }
        }

        if (visibleTag != null)
        {
            _isNavigatingFromMenu = true;
            var targetItem = SettingsNavigationView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == visibleTag);
            if (targetItem != null && SettingsNavigationView.SelectedItem != targetItem)
            {
                SettingsNavigationView.SelectedItem = targetItem;
            }
            _isNavigatingFromMenu = false;
        }
    }

    private void BringElementIntoView(FrameworkElement element)
    {
        if (element == null) return;

        var bringIntoViewOptions = new BringIntoViewOptions
        {
            AnimationDesired = true,
            VerticalAlignmentRatio = 0.0
        };

        element.StartBringIntoView(bringIntoViewOptions);
    }

    public async Task NavigateToUpdateSectionAsync()
    {
        var updateNavItem = SettingsNavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == "UpdateItem");

        if (updateNavItem != null)
        {
            SettingsNavigationView.SelectedItem = updateNavItem;
        }

        await Task.Delay(120);

        if (UpdateItem != null)
        {
            BringElementIntoView(UpdateItem);
        }

        await Task.Delay(120);

        CheckUpdateButton?.Focus(FocusState.Programmatic);
    }

    public async Task NavigateToCheckinSettingsAsync()
    {
        var checkinNavItem = SettingsNavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == "CheckinSettingsItem");

        if (checkinNavItem != null)
        {
            SettingsNavigationView.SelectedItem = checkinNavItem;
        }

        await Task.Delay(120);

        if (CheckinSettingsItem != null)
        {
            BringElementIntoView(CheckinSettingsItem);
        }
    }

    public async Task NavigateToNotificationPositionAsync()
    {
        var windowBehaviorNavItem = SettingsNavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == "WindowBehaviorItem");

        if (windowBehaviorNavItem != null)
        {
            SettingsNavigationView.SelectedItem = windowBehaviorNavItem;
        }

        await Task.Delay(120);

        if (NotificationPositionSettingRow != null)
        {
            BringElementIntoView(NotificationPositionSettingRow);
        }
    }

    #endregion
}
