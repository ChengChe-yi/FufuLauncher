/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Contracts.Services
{
    public interface ILocalSettingsService
    {
        /// <summary>
        /// 启动后台全量加载（幂等）。读取/写入路径也会自动触发，此方法用于在启动阶段
        /// 主动提前开始，使快照尽早就绪。
        /// </summary>
        void StartBackgroundLoad();

        Task<object?> ReadSettingAsync(string key);
        Task SaveSettingAsync<T>(string key, T value);
        Task RemoveSettingAsync(string key);
        Task ReInitializeAsync();
    }
}
