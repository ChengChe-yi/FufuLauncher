using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views.Controls;

public sealed partial class LaunchConfigurationPanel : UserControl
{
    public LaunchConfigurationViewModel ViewModel
    {
        get;
    }

    public LaunchConfigurationPanel()
    {
        ViewModel = App.GetService<LaunchConfigurationViewModel>();
        DataContext = ViewModel;
        InitializeComponent();
    }

    public async Task LoadAsync()
    {
        LoadError.IsOpen = false;
        try
        {
            await ViewModel.LoadAsync();
        }
        catch (Exception ex)
        {
            LoadError.Message = ex.Message;
            LoadError.IsOpen = true;
        }
    }

    private async void OnRetryClick(object sender, RoutedEventArgs e) => await LoadAsync();

    private void OnResolutionPresetChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedItem: ComboBoxItem { Tag: string resolution } } combo) return;
        ViewModel.SetResolutionPresetCommand.Execute(resolution);
        combo.SelectedIndex = -1;
    }

    private void OnOpenHdrClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement target)
        {
            GenshinHDRSettingPanel.ShowAt(target,
                Microsoft.UI.Xaml.Controls.Primitives.FlyoutPlacementMode.BottomEdgeAlignedLeft);
        }
    }

    private void OnIdentifyMonitorsClick(object sender, RoutedEventArgs e) => Helpers.MonitorHelper.IdentifyAll();
}