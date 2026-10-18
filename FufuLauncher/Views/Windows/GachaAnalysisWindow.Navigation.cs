/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class GachaAnalysisWindow
{
    private async void OnWindowFirstActivated(object sender, WindowActivatedEventArgs e)
    {
        if (!_firstActivated) return;
        _firstActivated = false;
        this.Activated -= OnWindowFirstActivated;

        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, async () =>
        {
            await ViewModel.LoadSavedGachaDataAsync();
            await ViewModel.InitializeSharedRolesAsync();
        });
    }

    private async void OnOverviewTabClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ShowOverviewAsync();
        UpdateTabIndicator();
    }

    private async void OnAnalysisTabClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.ShowAnalysisAsync();
        UpdateTabIndicator();
    }

    private void UpdateTabIndicator()
    {
        if (GachaTabPivot == null) return;

        var selectedIndex = ViewModel.IsHistorySelected
            ? 2
            : ViewModel.IsOverviewSelected
                ? 0
                : 1;
        _lastTabIndex = selectedIndex;
        if (GachaTabPivot.SelectedIndex == selectedIndex) return;

        _updatingTabSelection = true;
        try
        {
            GachaTabPivot.SelectedIndex = selectedIndex;
        }
        finally
        {
            _updatingTabSelection = false;
        }
    }

    private async void OnGachaTabPivotSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingTabSelection) return;

        var selectedIndex = GachaTabPivot.SelectedIndex;
        if (selectedIndex < 0 || selectedIndex == _lastTabIndex) return;

        _lastTabIndex = selectedIndex;

        Task showTask;
        UIElement? targetElement;

        if (selectedIndex == 0)
        {
            showTask = ViewModel.ShowOverviewAsync();
            targetElement = ViewModel.IsCardViewMode
                ? (UIElement)OverviewCardScrollViewer
                : OverviewListScrollViewer;
        }
        else if (selectedIndex == 1)
        {
            showTask = ViewModel.ShowAnalysisAsync();
            targetElement = AnalysisSectionRoot;
        }
        else
        {
            showTask = ViewModel.ShowHistoryAsync();
            targetElement = HistorySectionRoot;
        }

        if (targetElement != null)
        {
            DispatcherQueue.TryEnqueue(() => PlaySectionTransition(targetElement));
        }

        await showTask;

        if (selectedIndex == 1)
        {
            DispatcherQueue.TryEnqueue(PlayAnalysisChartAnimations);
        }

        UpdateTabIndicator();
    }

    private void OnListViewModeClick(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsCardViewMode)
        {
            ListViewModeButton.IsChecked = true;
            return;
        }

        ViewModel.IsCardViewMode = false;
        DispatcherQueue.TryEnqueue(() => PlaySectionTransition(OverviewListScrollViewer));
    }

    private void OnCardViewModeClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsCardViewMode)
        {
            CardViewModeButton.IsChecked = true;
            return;
        }

        ViewModel.IsCardViewMode = true;
        DispatcherQueue.TryEnqueue(() => PlaySectionTransition(OverviewCardScrollViewer));
    }

    private async void OnUidComboBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel.IsUpdatingRoleList || sender is not ComboBox combo) return;
        if (combo.SelectedItem is not string selected) return;

        System.Diagnostics.Debug.WriteLine(
            $"[Gacha] OnUidComboBoxSelectionChanged: selected={selected}, ViewModel.SelectedUid={ViewModel.SelectedUid}");

        if (selected == GachaAnalysisViewModel.AddNewUserItem)
        {
            var previous = ViewModel.SelectedUid;
            combo.SelectedItem = null;
            await ViewModel.AddNewUserCommand.ExecuteAsync(null);
        }
        else
        {
            await ViewModel.SwitchUidCommand.ExecuteAsync(selected);
        }
    }
}
