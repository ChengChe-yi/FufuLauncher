/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Activation;
using FufuLauncher.Messages;
using FufuLauncher.Views;
using Microsoft.UI.Xaml;

namespace FufuLauncher.ViewModels;

public partial class MainViewModel
{
    #region 置顶预设
    [ObservableProperty] private Visibility _presetCardVisibility = Visibility.Collapsed;
    [ObservableProperty] private ObservableCollection<PresetModel> _pinnedPresets = new();
    public bool IsPinnedPresetsEmpty => PinnedPresets.Count == 0;

    public IAsyncRelayCommand OpenPresetManagerCommand { get; }
    public IRelayCommand<PresetModel> QuickSwitchPresetCommand { get; }

    private async Task OpenPresetManagerAsync()
    {
        var window = new PresetManagerWindow();
        window.Closed += async (s, e) =>
        {
            await LoadPinnedPresetsAsync();
        };
        window.Activate();
    }

    private void RefreshPresetsActiveStatus(string activeId)
    {
        foreach (var preset in PinnedPresets)
        {
            preset.IsActive = (preset.Id == activeId);
        }
    }

    private string GetActivePresetIdFromFile()
    {
        string stateFile = Path.Combine(Helpers.AppPaths.PluginPresetsDir, "active_state.json");
        if (File.Exists(stateFile))
        {
            try
            {
                var stateContent = File.ReadAllText(stateFile);
                var stateDict = JsonSerializer.Deserialize<Dictionary<string, string>>(stateContent);
                if (stateDict != null && stateDict.TryGetValue("ActiveId", out var id))
                {
                    return id;
                }
            }
            catch { }
        }
        return string.Empty;
    }

    public async Task LoadPinnedPresetsAsync()
    {
        var pinnedIdsJson = await _localSettingsService.ReadSettingAsync("PinnedPresetIds");
        List<string> pinnedIds = new();
        if (pinnedIdsJson != null)
        {
            try { pinnedIds = JsonSerializer.Deserialize<List<string>>(pinnedIdsJson.ToString()); } catch { }
        }

        string presetsDir = Helpers.AppPaths.PluginPresetsDir;
        string activeId = GetActivePresetIdFromFile();

        await _dispatcherQueue.EnqueueAsync(() =>
        {
            PinnedPresets.Clear();
            if (Directory.Exists(presetsDir))
            {
                foreach (var file in Directory.GetFiles(presetsDir, "*.json"))
                {
                    if (file.EndsWith("active_state.json")) continue;
                    try
                    {
                        var content = File.ReadAllText(file);
                        var preset = JsonSerializer.Deserialize<PresetModel>(content);
                        if (preset != null && pinnedIds.Contains(preset.Id))
                        {
                            preset.FilePath = file;
                            preset.IsActive = (preset.Id == activeId);
                            PinnedPresets.Add(preset);
                        }
                    }
                    catch { }
                }
            }
            OnPropertyChanged(nameof(IsPinnedPresetsEmpty));
        });
    }

    private void QuickSwitchPreset(PresetModel targetPreset)
    {
        if (targetPreset == null) return;
        try
        {
            var pluginVM = new PluginSettingsViewModel();

            var fullPreset = pluginVM.AvailablePresets.FirstOrDefault(p => p.Id == targetPreset.Id);

            if (fullPreset != null)
            {
                pluginVM.SwitchPreset(fullPreset);
                RefreshPresetsActiveStatus(targetPreset.Id);
            }
            else
            {
                _notificationService.Show("预设切换失败", "未找到对应的预设配置", NotificationType.Error, 3000);
            }
        }
        catch (Exception ex)
        {
            _notificationService.Show("预设切换失败", ex.Message, NotificationType.Error, 3000);
        }
    }
    #endregion
}
