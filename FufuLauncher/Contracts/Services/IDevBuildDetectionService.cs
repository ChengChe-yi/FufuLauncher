/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Contracts.Services
{
    public interface IDevBuildDetectionService
    {
        bool IsDevBuild { get; }
        bool HasChecked { get; }
        bool IsDynamicBackgroundAllowed => IsDevBuild;
        Task<bool> DetectAsync(string serverVersion);
    }
}
