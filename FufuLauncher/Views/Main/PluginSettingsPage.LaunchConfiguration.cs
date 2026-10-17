using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class PluginSettingsPage
{
    public Visibility MainPluginButtonVisibility(int pluginIndex) =>
        pluginIndex == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void OnLaunchConfigurationFlyoutOpening(object sender, object e)
    {
        if (sender is not Flyout flyout) return;
        flyout.Content ??= new Controls.LaunchConfigurationPanel();
        if (flyout.Content is not Controls.LaunchConfigurationPanel panel || XamlRoot == null) return;
        panel.Width = Math.Min(820, Math.Max(240, XamlRoot.Size.Width - 80));
        panel.Height = Math.Min(560, Math.Max(180, XamlRoot.Size.Height - 100));
        _ = panel.LoadAsync();
    }
}
