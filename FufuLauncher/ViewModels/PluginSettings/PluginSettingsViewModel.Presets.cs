/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Security.Cryptography;
using System.Text.Json;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;

namespace FufuLauncher.ViewModels;

public partial class PluginSettingsViewModel
{
    #region 预设管理

    private string GetTargetDllHash()
    {
        if (!File.Exists(_dllPath)) return string.Empty;
        
        try
        {
            using var sha256 = SHA256.Create();
            using var stream = File.OpenRead(_dllPath);
            var hashBytes = sha256.ComputeHash(stream);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }
    private void ManagePresets(Dictionary<string, Dictionary<string, string>> currentIniData)
    {
        AvailablePresets.Clear();
        var currentHash = GetTargetDllHash();
        var stateFile = Path.Combine(_presetsDir, "active_state.json");
        string activePresetId = string.Empty;

        if (File.Exists(stateFile))
        {
            try
            {
                var stateContent = File.ReadAllText(stateFile);
                var stateDict = JsonSerializer.Deserialize<Dictionary<string, string>>(stateContent);
                if (stateDict != null && stateDict.TryGetValue("ActiveId", out var id))
                {
                    activePresetId = id;
                }
            }
            catch
            {
            }
        }

        try
        {
            if (Directory.Exists(_presetsDir))
            {
                var presetFiles = Directory.GetFiles(_presetsDir, "*.json").Where(f => !f.EndsWith("active_state.json"));
                PresetModel activeModel = null;

                foreach (var file in presetFiles)
                {
                    try
                    {
                        var content = File.ReadAllText(file);
                        var preset = JsonSerializer.Deserialize<PresetModel>(content);
                        if (preset != null)
                        {
                            preset.FilePath = file;
                            
                            bool presetModified = false;
                            
                            if (preset.ConfigData.Remove("General"))
                            {
                                presetModified = true;
                            }
                            
                            foreach (var sectionKey in preset.ConfigData.Keys.ToList())
                            {
                                if (currentIniData.TryGetValue(sectionKey, out var currentSectionData))
                                {
                                    preset.ConfigData[sectionKey].TryGetValue("Name", out var presetName);
                                    currentSectionData.TryGetValue("Name", out var currentName);
                                    
                                    if (presetName != currentName)
                                    {
                                        preset.ConfigData[sectionKey] = new Dictionary<string, string>(currentSectionData, StringComparer.OrdinalIgnoreCase);
                                        presetModified = true;
                                    }
                                }
                            }
                            
                            if (preset.DllHash != currentHash)
                            {
                                if (IsAutoCreatePresetEnabled)
                                {
                                    preset.IsLocked = true;
                                }
                                else
                                {
                                    preset.DllHash = currentHash;
                                    preset.IsLocked = false;
                                    SavePresetToFile(preset);
                                }
                            }
                            else
                            {
                                preset.IsLocked = false;
                                if (presetModified)
                                {
                                    SavePresetToFile(preset);
                                }
                            }

                            AvailablePresets.Add(preset);

                            if (preset.Id == activePresetId)
                            {
                                activeModel = preset;
                            }
                        }
                    }
                    catch { }
                }

                if (activeModel != null && activeModel.IsLocked)
                {
                    WeakReferenceMessenger.Default.Send(new NotificationMessage(
                        "插件变更",
                        "当前预设与最新插件版本不匹配，已自动生成新预设",
                        NotificationType.Warning,
                        5000
                    ));
                    activeModel = null;
                }

                if (activeModel == null)
                {
                    activeModel = CreateNewPreset("默认预设", currentIniData, currentHash);
                }

                CurrentPreset = activeModel;
                SaveActiveState();

                try
                {
                    ApplyPresetConfigToIni(CurrentPreset);
                }
                catch (Exception ex)
                {
                    WeakReferenceMessenger.Default.Send(new NotificationMessage(
                        "配置应用失败",
                        $"无法将预设写入配置文件，请检查权限\n详细信息: {ex.Message}",
                        NotificationType.Error,
                        6000
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "预设目录访问失败",
                $"无法访问预设目录\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }
    
    public void ClearAllPresets()
    {
        try
        {
            if (Directory.Exists(_presetsDir))
            {
                var files = Directory.GetFiles(_presetsDir, "*.json");
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
            AvailablePresets.Clear();
            CurrentPreset = null;
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "清除失败",
                $"无法清除预设文件\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }

    public PresetModel CreateNewPreset(string name, Dictionary<string, Dictionary<string, string>> data, string hash)
    {
        var cleanData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in data)
        {
            if (!kvp.Key.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                cleanData[kvp.Key] = new Dictionary<string, string>(kvp.Value, StringComparer.OrdinalIgnoreCase);
            }
        }

        var preset = new PresetModel
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            DllHash = hash,
            ConfigData = cleanData
        };

        preset.FilePath = Path.Combine(_presetsDir, $"{preset.Id}.json");
        SavePresetToFile(preset);
        AvailablePresets.Add(preset);
        return preset;
    }

    private void SavePresetToFile(PresetModel preset)
    {
        if (string.IsNullOrEmpty(preset.FilePath)) return;
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(preset.FilePath, JsonSerializer.Serialize(preset, options));
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "预设保存失败",
                $"无法保存预设文件，可能缺少写入权限\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }

    private void SaveActiveState()
    {
        if (CurrentPreset == null) return;
        try
        {
            var stateFile = Path.Combine(_presetsDir, "active_state.json");
            var stateDict = new Dictionary<string, string> { { "ActiveId", CurrentPreset.Id } };
            File.WriteAllText(stateFile, JsonSerializer.Serialize(stateDict));
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "状态保存失败",
                $"无法保存激活状态记录，可能缺少写入权限\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }

    private void ApplyPresetConfigToIni(PresetModel preset)
    {
        if (preset == null) return;

        var configData = new Dictionary<string, Dictionary<string, string>>(preset.ConfigData, StringComparer.OrdinalIgnoreCase);
        configData.Remove("General");
        _iniFile.UpdateMultiple(configData);
    }

    private void OnSettingValueChanged(string section, string key, string value)
    {
        if (CurrentPreset == null) return;
        
        if (section.Equals("General", StringComparison.OrdinalIgnoreCase)) return;

        if (!CurrentPreset.ConfigData.ContainsKey(section))
        {
            CurrentPreset.ConfigData[section] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        CurrentPreset.ConfigData[section][key] = value;
        SavePresetToFile(CurrentPreset);
    }

    public string GetPresetLockReason(PresetModel preset)
    {
        if (preset == null || !preset.IsLocked) return string.Empty;

        var currentHash = GetTargetDllHash();
        if (string.IsNullOrEmpty(preset.DllHash))
        {
            return "此预设没有记录插件 Hash，无法确认它是否适用于当前插件版本。";
        }

        if (string.IsNullOrEmpty(currentHash))
        {
            return "当前插件文件不存在或无法读取 Hash，无法确认此预设是否适用于当前插件版本。";
        }

        if (!string.Equals(preset.DllHash, currentHash, StringComparison.OrdinalIgnoreCase))
        {
            return "此预设记录的插件 Hash 与当前插件 Hash 不一致，可能来自旧版本或不同版本的插件。";
        }

        return "此预设当前被标记为锁定。";
    }

    public void ForceUnlockAndSwitchPreset(PresetModel targetPreset)
    {
        if (targetPreset == null) return;

        targetPreset.DllHash = GetTargetDllHash();
        targetPreset.IsLocked = false;
        SavePresetToFile(targetPreset);
        SwitchPreset(targetPreset);
    }

    public void SwitchPreset(PresetModel targetPreset)
    {
        if (targetPreset == null || targetPreset.IsLocked) return;

        CurrentPreset = targetPreset;
        SaveActiveState();
        
        try
        {
            ApplyPresetConfigToIni(CurrentPreset);
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "配置更新失败",
                $"切换预设时无法写入配置文件\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
        
        LoadConfiguration();
        
        WeakReferenceMessenger.Default.Send(new NotificationMessage(
            "预设已切换",
            $"当前预设: {targetPreset.Name}",
            NotificationType.Success,
            3000
        ));
    }
    
    public void DeletePreset(PresetModel targetPreset)
    {
        if (targetPreset == null || string.IsNullOrEmpty(targetPreset.FilePath)) return;
        
        try
        {
            if (File.Exists(targetPreset.FilePath))
            {
                File.Delete(targetPreset.FilePath);
            }
            
            AvailablePresets.Remove(targetPreset);
            
            if (CurrentPreset?.Id == targetPreset.Id)
            {
                LoadConfiguration();
            }
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "预设删除失败",
                $"无法删除指定的预设文件，文件可能被占用或权限不足\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }
    #endregion
}
