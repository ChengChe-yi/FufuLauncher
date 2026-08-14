/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    #region 设置项搜索

    private void BuildSearchIndex()
    {
        if (_searchIndex.Count > 0)
        {
            return;
        }

        foreach (var tag in _sectionTags)
        {
            if (FindName(tag) is not FrameworkElement section)
            {
                continue;
            }

            var sectionName = GetSectionDisplayName(tag);
            CollectSearchRows(section, tag, sectionName);
        }
    }

    private string GetSectionDisplayName(string tag)
    {
        var navItem = SettingsNavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == tag);

        return navItem?.Content?.ToString() ?? string.Empty;
    }

    private void CollectSearchRows(DependencyObject node, string sectionTag, string sectionName)
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i);

            if (child is FrameworkElement { Tag: "SettingsRow" } row)
            {
                var title = FindRowTitle(row);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    _searchIndex.Add(new SettingsSearchResult
                    {
                        Title = title!,
                        Section = sectionName,
                        SectionTag = sectionTag,
                        Element = row
                    });
                }
                continue;
            }

            CollectSearchRows(child, sectionTag, sectionName);
        }
    }

    private static string? FindRowTitle(DependencyObject node)
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(node, i);

            if (child is TextBlock { Tag: "RowTitle" } titleBlock)
            {
                return titleBlock.Text;
            }

            var nested = FindRowTitle(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void SettingsSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        try
        {
            BuildSearchIndex();
        }
        catch
        {
            // ignored
        }

        var query = sender.Text?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            sender.ItemsSource = null;
            return;
        }

        sender.ItemsSource = _searchIndex
            .Where(item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                           || item.Section.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToList();
    }

    private void SettingsSearchBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (args.SelectedItem is SettingsSearchResult result)
        {
            NavigateToSearchResult(result);
        }
    }

    private void SettingsSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        if (args.ChosenSuggestion is SettingsSearchResult chosen)
        {
            NavigateToSearchResult(chosen);
            return;
        }

        var query = args.QueryText?.Trim();
        if (string.IsNullOrEmpty(query))
        {
            return;
        }

        try
        {
            BuildSearchIndex();
        }
        catch
        {
            return;
        }

        var first = _searchIndex.FirstOrDefault(
            item => item.Title.Contains(query, StringComparison.OrdinalIgnoreCase));

        if (first != null)
        {
            NavigateToSearchResult(first);
        }
    }

    private void NavigateToSearchResult(SettingsSearchResult result)
    {
        if (result.Element == null)
        {
            return;
        }

        _isNavigatingFromMenu = true;
        _navLockTimer?.Stop();
        _navLockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _navLockTimer.Tick += (s, e) =>
        {
            ((DispatcherTimer)s).Stop();
            _isNavigatingFromMenu = false;
        };
        _navLockTimer.Start();

        var navItem = SettingsNavigationView.MenuItems
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == result.SectionTag);
        if (navItem != null && SettingsNavigationView.SelectedItem != navItem)
        {
            SettingsNavigationView.SelectedItem = navItem;
        }

        result.Element.StartBringIntoView(new BringIntoViewOptions
        {
            AnimationDesired = true,
            VerticalAlignmentRatio = 0.2
        });

        HighlightRow(result.Element);
    }

    private void HighlightRow(FrameworkElement row)
    {
        ClearHighlight();

        if (row is not Panel panel)
        {
            return;
        }

        _highlightedRow = row;
        _highlightedRowOriginalBackground = panel.Background;

        var color = ActualTheme == ElementTheme.Light
            ? Windows.UI.Color.FromArgb(0x24, 0x00, 0x00, 0x00)
            : Windows.UI.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF);
        panel.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(color);

        _highlightTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _highlightTimer.Tick += (s, e) =>
        {
            ((DispatcherTimer)s).Stop();
            ClearHighlight();
        };
        _highlightTimer.Start();
    }

    private void ClearHighlight()
    {
        _highlightTimer?.Stop();
        _highlightTimer = null;

        if (_highlightedRow is Panel previous)
        {
            previous.Background = _highlightedRowOriginalBackground;
        }

        _highlightedRow = null;
        _highlightedRowOriginalBackground = null;
    }

    #endregion
}
