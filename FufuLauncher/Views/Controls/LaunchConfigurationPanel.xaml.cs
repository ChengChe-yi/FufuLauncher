using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views.Controls;

public sealed partial class LaunchConfigurationPanel : UserControl
{
    public LaunchConfigurationViewModel ViewModel { get; }

    public LaunchConfigurationPanel()
    {
        ViewModel = App.GetService<LaunchConfigurationViewModel>();
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

    private async void OnOpenHdrClick(object sender, RoutedEventArgs e)
    {
        var dialog = new GenshinHDRLuminanceSettingDialog { XamlRoot = XamlRoot };
        await dialog.ShowAsync();
    }

    private void OnIdentifyMonitorsClick(object sender, RoutedEventArgs e) => Helpers.MonitorHelper.IdentifyAll();
}
