/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models.GameServer;
using FufuLauncher.Services.GameServer;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class MainPage
{
    #region 常规服/B服切换

    private async void SwitchToBilibili_Click(object sender, RoutedEventArgs e)
    {
        await PrepareAndSwitchServer(true);
    }

    private async void SwitchToOfficial_Click(object sender, RoutedEventArgs e)
    {
        await PrepareAndSwitchServer(false);
    }

    private async Task PrepareAndSwitchServer(bool toBilibili)
    {
        try
        {
            var localSettingsService = App.GetService<ILocalSettingsService>();
            var gamePathSetting = await localSettingsService.ReadSettingAsync("GameInstallationPath");

            var gameDir = gamePathSetting as string;
            if (!string.IsNullOrEmpty(gameDir))
            {
                gameDir = gameDir.Trim('"').Trim();
            }

            if (string.IsNullOrEmpty(gameDir) || !Directory.Exists(gameDir))
            {
                await ShowDialog("ErrorTitle".GetLocalized(), "Home_ErrNoGamePath".GetLocalized());
                return;
            }

            string configPath = Path.Combine(gameDir, "config.ini");
            if (!File.Exists(configPath))
            {
                string parentDir = Directory.GetParent(gameDir)?.FullName ?? "";
                string parentConfig = Path.Combine(parentDir, "config.ini");

                if (File.Exists(parentConfig))
                {
                    gameDir = parentDir;
                    configPath = parentConfig;
                }
                else
                {
                    await ShowDialog("ErrorTitle".GetLocalized(), "Home_ErrConfigNotFound".GetLocalized());
                    return;
                }
            }

            await PerformServerSwitch(gameDir, toBilibili);
        }
        catch (Exception ex)
        {
            await ShowDialog("ErrorTitle".GetLocalized(), $"{"Home_ErrSwitchException".GetLocalized()}: {ex.Message}");
        }
    }

    private async Task PerformServerSwitch(string gameDir, bool toBilibili)
    {
        try
        {
            var configurationService = App.GetService<GameServerConfigurationService>();
            var scheme = toBilibili ? GameServerScheme.FromPreset("Bili") : GameServerScheme.FromPreset("CN");

            configurationService.ApplyScheme(gameDir, scheme);

            await App.GetService<GameChannelSdkService>().EnsureSdkAndDeprecatedFilesAsync(gameDir, scheme);

            var serverName = toBilibili ? "Home_BilibiliServer".GetLocalized() : "Home_OfficialServer".GetLocalized();
            var action = toBilibili ? "Home_Deployed".GetLocalized() : "Home_Cleaned".GetLocalized();
            await ShowDialog("Home_SwitchSuccess".GetLocalized(), string.Format("Home_SwitchedTo_Format".GetLocalized(), serverName, action));
        }
        catch (Exception ex)
        {
            await ShowDialog("Home_SwitchFailed".GetLocalized(), ex.Message);
        }
    }

    private async Task ShowDialog(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "OkBtn".GetLocalized(),
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    #endregion
}
