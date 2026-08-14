/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Contracts.Services;
using FufuLauncher.Models.GameAnnouncement;
using FufuLauncher.Services;

namespace FufuLauncher.Views;

public static class GameAnnouncementLauncher
{
    public static async Task OpenAsync()
    {
        AnnouncementViewMode mode = AnnouncementViewMode.New;

        try
        {
            var saved = await App.GetService<ILocalSettingsService>()
                .ReadSettingAsync(LocalSettingsService.AnnouncementViewModeKey);

            if (saved is string modeStr && Enum.TryParse(modeStr, out AnnouncementViewMode parsed))
            {
                mode = parsed;
            }
        }
        catch
        {
            // ignored
        }

        if (mode == AnnouncementViewMode.Classic)
        {
            new AnnouncementWindow().Activate();
        }
        else
        {
            new GameAnnouncementWindow().Activate();
        }
    }
}
