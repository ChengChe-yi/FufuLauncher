using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    private async void OnOpenUpdateAnnouncementClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        button.IsEnabled = false;
        try
        {
            var updateInfoUrl = await App.GetService<IUpdateService>().GetUpdateAnnouncementUrlAsync();
            if (string.IsNullOrWhiteSpace(updateInfoUrl))
            {
                await ShowSafeDialogAsync(new ContentDialog
                {
                    Title = "UpdateNotification_Title".GetLocalized(),
                    Content = "GameAnnouncement_LoadFailed".GetLocalized(),
                    CloseButtonText = "GotItBtn".GetLocalized(),
                    XamlRoot = XamlRoot
                });
                return;
            }

            var isPreview = AppVersionHelper.IsPreviewBuild;
            var updateWindow = new UpdateNotificationWindow(updateInfoUrl, isPreview, showUpdateButton: false);
            updateWindow.Activate();
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
