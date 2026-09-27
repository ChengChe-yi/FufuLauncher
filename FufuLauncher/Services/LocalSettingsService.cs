/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Data.Repositories;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using Sentry;

namespace FufuLauncher.Services
{
    /// <summary>
    /// 本地设置存储。读取采用"懒加载 + 后台全量加载"两段式：
    /// <list type="number">
    /// <item>首次触碰时立即返回，不读表；</item>
    /// <item>同时在后台线程全量加载，填充快照；</item>
    /// <item>快照就绪前的读取按需单键查库（懒加载），命中后并入快照。</item>
    /// </list>
    /// 全量加载完成时与内存中已有的键做一致性检查：加载窗口内的本进程写入（保存/删除）
    /// 优先于快照值，其余保留内存值，避免覆盖刚写入的设置。
    /// </summary>
    public class LocalSettingsService : ILocalSettingsService
    {
        private const string _defaultApplicationDataFolder = "FufuLauncher/ApplicationData";
        private const string _defaultLocalSettingsDb = "LocalSettings.db";

        private readonly LocalSettingsRepository _repository;

        private readonly ConcurrentDictionary<string, string> _settings = new();

        /// <summary>加载窗口内确认不存在的键，避免同一缺失键反复查库。</summary>
        private readonly ConcurrentDictionary<string, byte> _missingDuringLoad = new();

        /// <summary>加载窗口内被删除的键；合并快照时不得重新填回。</summary>
        private readonly ConcurrentDictionary<string, byte> _removedDuringLoad = new();

        private readonly SemaphoreSlim _fullLoadGate = new(1, 1);

        /// <summary>全量快照是否已就绪。就绪后未命中即代表该键确实不存在。</summary>
        private volatile bool _fullLoadCompleted;

        private Task? _backgroundLoadTask;
        private bool _dirErrorNotified;

        public const string BackgroundServerKey = "BackgroundServer";
        public const string IsBackgroundEnabledKey = "IsBackgroundEnabled";
        public const string LastAnnouncedVersionKey = "LastAnnouncedVersion";

        public const string LastAnnouncedPreviewVersionKey = "LastAnnouncedPreviewVersion";

        public const string LastAnnouncementUrlKey = "LastAnnouncementUrl";

        public const string HasShownSecurityWarningKey = "HasShownSecurityWarning";

        public const string HasDismissedFpsWarningKey = "HasDismissedFpsWarning";

        public const string AnnouncementViewModeKey = "AnnouncementViewMode";

        public const string AnnouncementRegionKey = "AnnouncementRegion";

        private readonly JsonSerializerOptions _jsonOptions;

        public LocalSettingsService(LocalSettingsRepository repository)
        {
            _repository = repository;

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        /// <summary>
        /// 启动后台全量加载。幂等，可重复调用；读取/写入路径也会在首次触碰时自动触发。
        /// </summary>
        public void StartBackgroundLoad()
        {
            if (_backgroundLoadTask is not null)
                return;

            _backgroundLoadTask = Task.Run(RunBackgroundLoadAsync);
        }

        private async Task RunBackgroundLoadAsync()
        {
            try
            {
                await _fullLoadGate.WaitAsync();
                try
                {
                    if (_fullLoadCompleted)
                        return;

                    await LoadAllCoreAsync();
                }
                finally
                {
                    _fullLoadGate.Release();
                }
            }
            catch (Exception ex)
            {
                // 后台任务无人 await，异常不能外泄为未观察异常；保持懒加载模式即可
                Debug.WriteLine($"LocalSettingsService: 后台全量加载异常 - {ex.Message}");
            }
        }

        /// <summary>全量加载并合并进快照。调用方需持有 <see cref="_fullLoadGate"/>。</summary>
        private async Task LoadAllCoreAsync()
        {
            Debug.WriteLine("LocalSettingsService: 开始全量加载设置");

            try
            {
                var dir = Path.GetDirectoryName(AppPaths.LocalSettingsDb);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalSettingsService: 创建配置目录失败 - {ex.Message}");

                // 目录不可用：保持懒加载模式，只提示一次；后续读写会各自失败并记日志。
                if (!_dirErrorNotified)
                {
                    _dirErrorNotified = true;
                    WeakReferenceMessenger.Default.Send(new NotificationMessage(
                        "Settings_DirCreateFailed".GetLocalized(),
                        string.Format("Settings_DirCreateFailedMsg".GetLocalized(), ex.Message),
                        NotificationType.Error,
                        4000
                    ));
                }
                return;
            }

            var (success, all) = await _repository.TryGetAllSettingsAsync();
            if (!success)
            {
                // 查询失败不能当作"表为空"，否则会把空结果当成权威快照。保持懒加载模式。
                Debug.WriteLine("LocalSettingsService: 全量加载失败，继续使用懒加载");
                return;
            }

            int added = 0, kept = 0, skippedRemoved = 0;
            foreach (var (key, value) in all)
            {
                // 加载窗口内被删除的键：磁盘快照可能仍含旧值，不得重新填回
                if (_removedDuringLoad.ContainsKey(key))
                {
                    skippedRemoved++;
                    continue;
                }

                if (_settings.TryGetValue(key, out var existing))
                {
                    // 加载窗口内发生过懒加载或写入，内存值不旧于快照值，保留并记录差异
                    if (!string.Equals(existing, value, StringComparison.Ordinal))
                    {
                        kept++;
                        SettingsLog.Write($"LocalSettingsService: '{key}' 内存值与快照不一致，保留内存值");
                    }
                    continue;
                }

                if (_settings.TryAdd(key, value))
                    added++;
            }

            _missingDuringLoad.Clear();
            _removedDuringLoad.Clear();
            _fullLoadCompleted = true;
            Debug.WriteLine($"LocalSettingsService: 全量加载完成，共 {all.Count} 项（新增 {added}，保留内存值 {kept}，跳过已删除 {skippedRemoved}）");
        }

        public async Task<object?> ReadSettingAsync(string key)
        {
            StartBackgroundLoad();

            if (_settings.TryGetValue(key, out var storedValue))
            {
                SettingsLog.Write($"LocalSettingsService: 读取 {key}");
                return Deserialize(storedValue);
            }

            // 已删除或快照已就绪仍未命中 → 该键确实不存在
            if (_fullLoadCompleted || _removedDuringLoad.ContainsKey(key) || _missingDuringLoad.ContainsKey(key))
            {
                SettingsLog.Write($"LocalSettingsService: 读取 '{key}' 未找到");
                return null;
            }

            // 懒加载：只取这一个键，避免为单次读取拉全表
            var (success, found, lazyValue) = await _repository.TryGetSettingAsync(key);
            if (success && found)
            {
                _settings[key] = lazyValue;
                SettingsLog.Write($"LocalSettingsService: 懒加载 {key}");
                return Deserialize(lazyValue);
            }

            if (success)
                _missingDuringLoad.TryAdd(key, 0);
            else
                Debug.WriteLine($"LocalSettingsService: 懒加载 '{key}' 查询失败");

            SettingsLog.Write($"LocalSettingsService: 读取 '{key}' 未找到");
            return null;
        }

        private object? Deserialize(string storedValue)
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<object>(storedValue, _jsonOptions);

                if (deserialized is JsonElement jsonElement)
                {
                    return jsonElement.ValueKind switch
                    {
                        JsonValueKind.String => jsonElement.GetString(),
                        JsonValueKind.Number => jsonElement.GetDouble(),
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.Array => jsonElement.EnumerateArray().ToArray(),
                        JsonValueKind.Object => jsonElement,
                        _ => storedValue
                    };
                }

                return deserialized;
            }
            catch (JsonException)
            {
                return storedValue;
            }
        }

        public async Task SaveSettingAsync<T>(string key, T value)
        {
            StartBackgroundLoad();

            var json = JsonSerializer.Serialize(value, _jsonOptions);

            SettingsLog.Write($"LocalSettingsService: 保存{key}");

            try
            {
                await _repository.UpsertSettingAsync(key, json);

                // 写穿缓存：并入快照，且全量加载合并时会保留此值
                _settings[key] = json;
                _missingDuringLoad.TryRemove(key, out _);
                _removedDuringLoad.TryRemove(key, out _);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalSettingsService: 保存设置失败 - {ex.Message}");
                SentrySdk.CaptureException(ex, scope =>
                {
                    scope.SetTag("source", "LocalSettingsService");
                    scope.SetTag("settingKey", key);
                });
                WeakReferenceMessenger.Default.Send(new NotificationMessage(
                    "Settings_ConfigSaveFailed".GetLocalized(),
                    string.Format("Settings_ConfigSaveFailedMsg".GetLocalized(), ex.Message)
                        + Environment.NewLine
                        + string.Format("Settings_ConfigSaveFailedPathHint".GetLocalized(), AppPaths.LocalSettingsDb),
                    NotificationType.Error,
                    5000));
            }
        }

        public async Task RemoveSettingAsync(string key)
        {
            StartBackgroundLoad();

            _settings.TryRemove(key, out _);
            _missingDuringLoad.TryAdd(key, 0);
            _removedDuringLoad.TryAdd(key, 0);

            await _repository.DeleteSettingAsync(key);
        }

        /// <summary>
        /// 丢弃当前快照并同步重建（等待完成）。用于首次运行接受协议后切换数据目录等
        /// 明确需要"返回时快照即最新"的场景。
        /// </summary>
        public async Task ReInitializeAsync()
        {
            await _fullLoadGate.WaitAsync();
            try
            {
                _settings.Clear();
                _missingDuringLoad.Clear();
                _removedDuringLoad.Clear();
                _fullLoadCompleted = false;

                await LoadAllCoreAsync();
            }
            finally
            {
                _fullLoadGate.Release();
            }
        }
    }
}
