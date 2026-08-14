/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;

namespace FufuLauncher.ViewModels;

public partial class PluginSettingsViewModel
{
    #region HWID 授权与开发者功能

    private static bool _isHwidAuthorized = false;
    private static bool _hasCheckedHwid = false;

    private async Task<bool> CheckHwidAuthorizationAsync()
    {
        if (_hasCheckedHwid && _isHwidAuthorized) return true;
        
        var authorizationService = App.GetService<Services.DeveloperAuthorizationService>();
        _isHwidAuthorized = authorizationService is not null && await authorizationService.IsAuthorizedAsync();
        _hasCheckedHwid = true;
        System.Diagnostics.Debug.WriteLine($"[HWID_DEBUG] 授权结果: {_isHwidAuthorized}");
        return _isHwidAuthorized;
    }

public async Task TriggerBackgroundAuthCheckAsync()
{
    if (_hasCheckedHwid && _isHwidAuthorized) return;

    bool isAuthorized = await CheckHwidAuthorizationAsync();
    var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

    if (!isAuthorized)
    {
        string avatarDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar");
        string avatarEnabledPath = Path.Combine(avatarDir, "Avatar.dll");
        string avatarDisabledPath = Path.Combine(avatarDir, "Avatar.disabled");
        
        if (File.Exists(avatarEnabledPath))
        {
            try { File.Move(avatarEnabledPath, avatarDisabledPath); } catch {}
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() => 
                {
                    _isAvatarPluginEnabled = false;
                    OnPropertyChanged(nameof(IsAvatarPluginEnabled));
                    RefreshUIState();
                });
            }
        }
    }
    else
    {
        if (dispatcher != null)
        {
            dispatcher.TryEnqueue(() => LoadConfiguration());
        }
        else
        {
            LoadConfiguration();
        }
    }
}
    private async Task InitializeAuthAndReloadAsync()
    {
        if (!_hasCheckedHwid)
        {
            await CheckHwidAuthorizationAsync();
            
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            if (dispatcher != null)
            {
                dispatcher.TryEnqueue(() => LoadConfiguration());
            }
            else
            {
                LoadConfiguration();
            }
        }
    }
    

    public async Task StartAsynchronousAuthAsync()
    {
        bool isAuthorized = await CheckHwidAuthorizationAsync();
        
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        if (dispatcher != null)
        {
            dispatcher.TryEnqueue(() => HandleAuthResult(isAuthorized));
        }
        else
        {
            HandleAuthResult(isAuthorized);
        }
    }
    
    private void HandleAuthResult(bool isAuthorized)
    {
        if (isAuthorized)
        {
            LoadConfiguration();
        }
        else
        {
            string avatarDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar");
            string avatarEnabledPath = Path.Combine(avatarDir, "Avatar.dll");
            string avatarDisabledPath = Path.Combine(avatarDir, "Avatar.disabled");

            if (File.Exists(avatarEnabledPath))
            {
                try
                {
                    File.Move(avatarEnabledPath, avatarDisabledPath);
                }
                catch { }
                
                _isAvatarPluginEnabled = false;
                OnPropertyChanged(nameof(IsAvatarPluginEnabled));
                RefreshUIState();
            }
        }
    }
    private async Task VerifyAndApplyDevFeaturesAsync()
    {
        bool isAuthorized = await CheckHwidAuthorizationAsync();
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

        Action action = () =>
        {
            if (isAuthorized)
            {
                SaveDevFeaturesSetting(true);
                LoadConfiguration();
            }
            else
            {
                WeakReferenceMessenger.Default.Send(new NotificationMessage(
                    "权限验证失败",
                    "您当前不具备开发者功能权限，无法开启该功能",
                    NotificationType.Error,
                    4000
                ));
                
                _isDevFeaturesEnabled = false;
                OnPropertyChanged(nameof(IsDevFeaturesEnabled));
                SaveDevFeaturesSetting(false);
                LoadConfiguration();
            }
        };

        if (dispatcher != null) dispatcher.TryEnqueue(() => action());
        else action();
    }

    private async Task VerifyAndApplyDevFeaturesOnStartupAsync()
    {
        bool isAuthorized = await CheckHwidAuthorizationAsync();
        if (!isAuthorized)
        {
            var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
            Action action = () =>
            {
                _isDevFeaturesEnabled = false;
                OnPropertyChanged(nameof(IsDevFeaturesEnabled));
                SaveDevFeaturesSetting(false);
                LoadConfiguration();
            };

            if (dispatcher != null) dispatcher.TryEnqueue(() => action());
            else action();
        }
    }

    private void SaveDevFeaturesSetting(bool isEnabled)
    {
        var localSettings = App.GetService<FufuLauncher.Contracts.Services.ILocalSettingsService>();
        if (localSettings != null)
        {
            _ = localSettings.SaveSettingAsync("IsDevFeaturesEnabled", isEnabled);
        }
    }

    #endregion
}
