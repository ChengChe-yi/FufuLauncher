/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Contracts.Services
{
    public interface ILocalSettingsService
    {
        //启动后台全量加载
        void StartBackgroundLoad();

        Task<object?> ReadSettingAsync(string key);
        Task SaveSettingAsync<T>(string key, T value);

        Task<bool> TrySaveSettingAsync<T>(string key, T value);

        Task RemoveSettingAsync(string key);

        Task InvalidateAndReloadAsync();
    }
}
