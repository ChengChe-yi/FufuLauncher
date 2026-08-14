/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Models.GameAnnouncement;

namespace FufuLauncher.Services.GameAnnouncement
{
    public interface IGameAnnouncementService
    {
        Task<AnnouncementWrapper?> GetAnnouncementsAsync(
            string languageCode,
            AnnouncementRegion region,
            bool forceRefresh,
            CancellationToken token = default);
    }
}
