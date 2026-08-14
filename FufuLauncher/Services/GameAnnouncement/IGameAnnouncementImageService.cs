/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.GameAnnouncement
{
    public interface IGameAnnouncementImageService
    {
        Task<byte[]?> GetImageBytesAsync(string? url, CancellationToken token = default);
    }
}
