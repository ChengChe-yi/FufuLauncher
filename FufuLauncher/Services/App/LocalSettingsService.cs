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
    public class LocalSettingsService : ILocalSettingsService
    {
        private const string _defaultApplicationDataFolder = "FufuLauncher/ApplicationData";
        private const string _defaultLocalSettingsDb = "LocalSettings.db";

        private static readonly TimeSpan LoadRetryBackoff = TimeSpan.FromSeconds(30);

        private readonly LocalSettingsRepository _repository;

        private readonly ConcurrentDictionary<string, string> _settings = new();


        private readonly ConcurrentDictionary<string, byte> _missingDuringLoad = new();


        private readonly ConcurrentDictionary<string, byte> _removedDuringLoad = new();


        private readonly object _stateLock = new();


        private readonly SemaphoreSlim _writeGate = new(1, 1);


        private readonly SemaphoreSlim _fullLoadGate = new(1, 1);


        private bool _fullLoadCompleted;


        private Task? _backgroundLoadTask;

        private DateTime? _lastLoadFailureUtc;

        private bool _dirErrorNotified;

        public const string BackgroundServerKey = "BackgroundServer";
        public const string IsBackgroundEnabledKey = "IsBackgroundEnabled";
        public const string IsStartupEnabledKey = "IsStartupEnabled";
        public const string LastAnnouncedVersionKey = "LastAnnouncedVersion";

        public const string LastAnnouncedPreviewVersionKey = "LastAnnouncedPreviewVersion";

        public const string LastAnnouncementUrlKey = "LastAnnouncementUrl";

        public const string SuppressAnnouncementInGameKey = "IsSuppressAnnouncementInGameEnabled";

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


        public void StartBackgroundLoad()
        {
            lock (_stateLock)
            {
                if (_fullLoadCompleted)
                    return;


                if (_backgroundLoadTask is { IsCompleted: false })
                    return;


                if (_lastLoadFailureUtc is { } failedAt &&
                    DateTime.UtcNow - failedAt < LoadRetryBackoff)
                    return;

                _backgroundLoadTask = Task.Run(RunBackgroundLoadAsync);
            }
        }

        private async Task RunBackgroundLoadAsync()
        {
            try
            {
                await _fullLoadGate.WaitAsync();
                try
                {
                    lock (_stateLock)
                    {
                        if (_fullLoadCompleted)
                            return;
                    }

                    await LoadAllCoreAsync();
                }
                finally
                {
                    _fullLoadGate.Release();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LocalSettingsService: 后台全量加载异常 - {ex.Message}");
            }
            finally
            {
                lock (_stateLock)
                {
                    if (!_fullLoadCompleted)
                    {
                        _backgroundLoadTask = null;
                        _lastLoadFailureUtc = DateTime.UtcNow;
                    }
                }
            }
        }


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
                Debug.WriteLine("LocalSettingsService: 全量加载失败，继续使用懒加载");
                return;
            }

            int added = 0, kept = 0, skippedRemoved = 0;
            lock (_stateLock)
            {
                foreach (var (key, value) in all)
                {
                    if (_removedDuringLoad.ContainsKey(key))
                    {
                        skippedRemoved++;
                        continue;
                    }

                    if (_settings.TryGetValue(key, out var existing))
                    {
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
                _lastLoadFailureUtc = null;
            }

            Debug.WriteLine(
                $"LocalSettingsService: 全量加载完成，共 {all.Count} 项（新增 {added}，保留内存值 {kept}，跳过已删除 {skippedRemoved}）");
        }


        public async Task<bool> InvalidateAndReloadAsync()
        {
            await _fullLoadGate.WaitAsync();
            try
            {
                await _writeGate.WaitAsync();
                try
                {
                    var (success, all) = await _repository.TryGetAllSettingsAsync();
                    if (!success)
                    {
                        Debug.WriteLine("LocalSettingsService: 整表替换后重载失败，缓存保持原状");
                        return false;
                    }

                    lock (_stateLock)
                    {
                        _settings.Clear();
                        foreach (var (key, value) in all)
                            _settings[key] = value;

                        _missingDuringLoad.Clear();
                        _removedDuringLoad.Clear();
                        _fullLoadCompleted = true;
                        _lastLoadFailureUtc = null;
                    }

                    Debug.WriteLine($"LocalSettingsService: 缓存已按数据库重建，共 {all.Count} 项");
                    return true;
                }
                finally
                {
                    _writeGate.Release();
                }
            }
            finally
            {
                _fullLoadGate.Release();
            }
        }

        public async Task<object?> ReadSettingAsync(string key)
        {
            StartBackgroundLoad();

            string? fromCache;
            bool foundInCache;
            bool knownAbsent;

            lock (_stateLock)
            {
                foundInCache = _settings.TryGetValue(key, out fromCache);
                knownAbsent = !foundInCache && IsKnownAbsentLocked(key);
            }

            if (foundInCache)
            {
                SettingsLog.Write($"LocalSettingsService: 读取 {key}");
                return Deserialize(fromCache!);
            }

            if (knownAbsent)
            {
                SettingsLog.Write($"LocalSettingsService: 读取 '{key}' 未找到");
                return null;
            }


            var (success, found, lazyValue) = await _repository.TryGetSettingAsync(key);


            lock (_stateLock)
            {
                if (_settings.TryGetValue(key, out var current))
                {
                    SettingsLog.Write($"LocalSettingsService: 懒加载 {key} 期间已有更新值，以缓存为准");
                    return Deserialize(current);
                }

                if (_removedDuringLoad.ContainsKey(key) || _fullLoadCompleted)
                {
                    SettingsLog.Write($"LocalSettingsService: 读取 '{key}' 未找到");
                    return null;
                }

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
        }


        private bool IsKnownAbsentLocked(string key) =>
            _fullLoadCompleted || _removedDuringLoad.ContainsKey(key) || _missingDuringLoad.ContainsKey(key);

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
            await TrySaveSettingAsync(key, value);
        }


        public async Task<bool> TrySaveSettingAsync<T>(string key, T value)
        {
            StartBackgroundLoad();

            var json = JsonSerializer.Serialize(value, _jsonOptions);

            SettingsLog.Write($"LocalSettingsService: 保存{key}");

            await _writeGate.WaitAsync();
            try
            {
                await _repository.UpsertSettingAsync(key, json);

                lock (_stateLock)
                {
                    _settings[key] = json;
                    _missingDuringLoad.TryRemove(key, out _);
                    _removedDuringLoad.TryRemove(key, out _);
                }

                return true;
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
                return false;
            }
            finally
            {
                _writeGate.Release();
            }
        }

        public async Task RemoveSettingAsync(string key)
        {
            StartBackgroundLoad();

            await _writeGate.WaitAsync();
            try
            {
                bool deleted = await _repository.DeleteSettingAsync(key);
                if (!deleted)
                {
                    Debug.WriteLine($"LocalSettingsService: 删除 '{key}' 未生效，缓存保持不变");
                    return;
                }

                lock (_stateLock)
                {
                    _settings.TryRemove(key, out _);

                    if (!_fullLoadCompleted)
                    {
                        _removedDuringLoad.TryAdd(key, 0);
                        _missingDuringLoad.TryAdd(key, 0);
                    }
                }
            }
            finally
            {
                _writeGate.Release();
            }
        }
    }
}