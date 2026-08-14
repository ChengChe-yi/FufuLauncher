/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;

namespace FufuLauncher.ViewModels;

public partial class PluginSettingsViewModel
{
    #region 配置加载


public void LoadConfiguration()
    {
        Settings.Clear();

        if (SelectedPluginIndex == 2)
        {
            PluginName = "千星奇域头像替换";
            PluginDescription = "注意：开启此功能会自动禁用FPS插件，两者不可同时开启，替换头像是永久性的";
            PluginDeveloper = "不可用";
            LastModifiedDate = "不可用";
            AvailablePresets.Clear();
            CurrentPreset = null;
            return;
        }

        if (!File.Exists(_iniPath))
        {
            PluginName = SelectedPluginIndex == 0 ? "未安装 FuFuPlugin" : "未安装 FPS 插件";
            PluginDescription = "请确保Plugins目录下存在对应的文件夹及config.ini文件";
            return;
        }

        try
        {
            var fileInfo = new FileInfo(_iniPath);
            LastModifiedDate = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");

            var configData = _iniFile.ReadAll();
            if (configData.TryGetValue("General", out var generalSection))
            {
                PluginName = generalSection.GetValueOrDefault("Name", "未知插件");
                PluginDescription = generalSection.GetValueOrDefault("Description", "无描述");
                PluginDeveloper = generalSection.GetValueOrDefault("Developer", "未知作者");
            }

            ManagePresets(configData);

            bool isDevZone = false;
            foreach (var section in _iniFile.ReadAll())
            {
                if (section.Key.Equals("General", StringComparison.OrdinalIgnoreCase)) continue;

                if (section.Key.Equals("DEV", StringComparison.OrdinalIgnoreCase))
                {
                    isDevZone = true;
                    continue;
                }

                if (isDevZone && !_isDevFeaturesEnabled)
                {
                    continue;
                }

                var dic = section.Value;
                var iniName = dic.GetValueOrDefault("Name", section.Key);
                var name = iniName;
                if (SelectedPluginIndex == 0)
                {
                    // Translations under "Plugin_<SectionKey>" were authored specifically for
                    // FuFuPlugin's config.ini. Other plugins (FPS, Avatar) can reuse the same
                    // section names for unrelated settings, so only apply this lookup for
                    // FuFuPlugin to avoid showing a mistranslated label on another plugin's setting.
                    var localizationKey = $"Plugin_{section.Key}";
                    var localizedName = localizationKey.GetLocalized();
                    name = localizedName != localizationKey ? localizedName : iniName;
                }
                var type = dic.GetValueOrDefault("Type", "string");
                var value = dic.GetValueOrDefault("Value", "");
                var help = dic.GetValueOrDefault("help", "");
                
                var settingItem = new PluginSettingItem(_iniFile, section.Key, name, type, value, help, OnSettingValueChanged, UseKeyListInput);
                Settings.Add(settingItem);
            }
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "配置读取失败",
                $"无法读取插件配置文件。\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }
    #endregion
}
