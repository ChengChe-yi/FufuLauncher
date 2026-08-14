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
    #region 插件状态与路径管理

    private bool _isMainPluginEnabled;
    public bool IsMainPluginEnabled
    {
        get => _isMainPluginEnabled;
        set
        {
            if (_isMainPluginEnabled != value)
            {
                ChangeMainPluginState(value);
            }
        }
    }

    private bool _isFpsPluginEnabled;
    public bool IsFpsPluginEnabled
    {
        get => _isFpsPluginEnabled;
        set
        {
            if (_isFpsPluginEnabled != value)
            {
                ChangeFpsPluginState(value);
            }
        }
    }

    private bool _isAvatarPluginEnabled;
    public bool IsAvatarPluginEnabled
    {
        get => _isAvatarPluginEnabled;
        set
        {
            if (_isAvatarPluginEnabled != value)
            {
                ChangeAvatarPluginState(value);
            }
        }
    }

    public Microsoft.UI.Xaml.Visibility SettingsOverlayVisibility => 
        (SelectedPluginIndex == 0 && !_isMainPluginEnabled) || (SelectedPluginIndex == 1 && !_isFpsPluginEnabled) || (SelectedPluginIndex == 2 && !_isAvatarPluginEnabled) 
            ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public bool IsSettingsInteractable => (SelectedPluginIndex == 0 && _isMainPluginEnabled) || (SelectedPluginIndex == 1 && _isFpsPluginEnabled) || (SelectedPluginIndex == 2 && _isAvatarPluginEnabled);

    public string OverlayWarningText
    {
        get
        {
            if (SelectedPluginIndex == 0) return "已被禁用，请启用主插件才能调试配置";
            if (SelectedPluginIndex == 1) return "已被禁用，请启用FPS插件才能调试插件配置";
            if (SelectedPluginIndex == 2) return "已被禁用，该插件存在安全风险，无法启用";
            return string.Empty;
        }
    }

private void CheckPluginStates()
{
    string fpsDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "FPS");
    string fpsEnabledPath = Path.Combine(fpsDir, "FPS.dll");
    string fpsDisabledPath = Path.Combine(fpsDir, "FPS.disabled");
    
    string avatarDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar");
    string avatarEnabledPath = Path.Combine(avatarDir, "Avatar.dll");
    string avatarDisabledPath = Path.Combine(avatarDir, "Avatar.disabled");
    
    string mainDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "FuFuPlugin");
    string mainEnabledPath = Path.Combine(mainDir, "FufuLauncher.UnlockerIsland.dll");
    string mainDisabledPath = Path.Combine(mainDir, "FufuLauncher.UnlockerIsland.disabled");

    if (File.Exists(mainEnabledPath) && File.Exists(mainDisabledPath))
    {
        try { File.Delete(mainDisabledPath); } catch { }
    }

    _isMainPluginEnabled = File.Exists(mainEnabledPath);
    OnPropertyChanged(nameof(IsMainPluginEnabled));

    if (File.Exists(fpsEnabledPath) && File.Exists(fpsDisabledPath))
    {
        try { File.Delete(fpsDisabledPath); } catch { }
    }
    
    if (File.Exists(avatarEnabledPath) && File.Exists(avatarDisabledPath))
    {
        try { File.Delete(avatarDisabledPath); } catch { }
    }

    bool fpsEnabled = File.Exists(fpsEnabledPath);
    bool avatarEnabled = File.Exists(avatarEnabledPath);
    
    bool newFpsState = fpsEnabled;
    bool newAvatarState = avatarEnabled;

    if (fpsEnabled && avatarEnabled)
    {
        try
        {
            File.Move(fpsEnabledPath, fpsDisabledPath);
            File.Move(avatarEnabledPath, avatarDisabledPath);
        }
        catch { }
        
        newFpsState = false;
        newAvatarState = false;
    }

    if (_isFpsPluginEnabled != newFpsState)
    {
        _isFpsPluginEnabled = newFpsState;
        OnPropertyChanged(nameof(IsFpsPluginEnabled));
    }

    if (_isAvatarPluginEnabled != newAvatarState)
    {
        _isAvatarPluginEnabled = newAvatarState;
        OnPropertyChanged(nameof(IsAvatarPluginEnabled));
    }

    RefreshUIState();
}

    
    private void ChangeMainPluginState(bool enable)
    {
        string mainDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "FuFuPlugin");
        string enabledPath = Path.Combine(mainDir, "FufuLauncher.UnlockerIsland.dll");
        string disabledPath = Path.Combine(mainDir, "FufuLauncher.UnlockerIsland.disabled");

        if (!Directory.Exists(mainDir)) Directory.CreateDirectory(mainDir);

        try
        {
            if (enable && File.Exists(disabledPath))
            {
                File.Move(disabledPath, enabledPath);
            }
            else if (!enable && File.Exists(enabledPath))
            {
                File.Move(enabledPath, disabledPath);
            }
        
            SetProperty(ref _isMainPluginEnabled, enable, nameof(IsMainPluginEnabled));
            RefreshUIState();
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "状态切换失败",
                $"无法修改文件后缀名。\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }

    private void ChangeFpsPluginState(bool enable)
    {
        if (enable && IsAvatarPluginEnabled)
        {
            IsAvatarPluginEnabled = false;
        }

        string fpsDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "FPS");
        string enabledPath = Path.Combine(fpsDir, "FPS.dll");
        string disabledPath = Path.Combine(fpsDir, "FPS.disabled");

        try
        {
            if (enable && File.Exists(disabledPath))
            {
                File.Move(disabledPath, enabledPath);
            }
            else if (!enable && File.Exists(enabledPath))
            {
                File.Move(enabledPath, disabledPath);
            }
            
            SetProperty(ref _isFpsPluginEnabled, enable, nameof(IsFpsPluginEnabled));
            RefreshUIState();
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "状态切换失败",
                $"无法修改插件文件后缀名。\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }

    private async void ChangeAvatarPluginState(bool enable)
    {
        if (enable)
        {
            bool isAuthorized = await CheckHwidAuthorizationAsync();
            if (!isAuthorized)
            {
                WeakReferenceMessenger.Default.Send(new NotificationMessage(
                    "认证未通过",
                    "已被禁用，该插件存在安全风险，无法启用",
                    NotificationType.Error,
                    6000
                ));
                var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
                if (dispatcher != null)
                {
                    dispatcher.TryEnqueue(() => { SetProperty(ref _isAvatarPluginEnabled, false, nameof(IsAvatarPluginEnabled)); });
                }
                else
                {
                    SetProperty(ref _isAvatarPluginEnabled, false, nameof(IsAvatarPluginEnabled));
                }
                
                string avatarDirCheck = Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar");
                string enabledPathCheck = Path.Combine(avatarDirCheck, "Avatar.dll");
                string disabledPathCheck = Path.Combine(avatarDirCheck, "Avatar.disabled");
                if (File.Exists(enabledPathCheck))
                {
                    try { File.Move(enabledPathCheck, disabledPathCheck); } catch { }
                }
                return;
            }

            if (IsFpsPluginEnabled)
            {
                IsFpsPluginEnabled = false;
            }
        }

        string avatarDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "Avatar");
        string enabledPath = Path.Combine(avatarDir, "Avatar.dll");
        string disabledPath = Path.Combine(avatarDir, "Avatar.disabled");

        if (!Directory.Exists(avatarDir)) Directory.CreateDirectory(avatarDir);

        try
        {
            if (enable && File.Exists(disabledPath))
            {
                File.Move(disabledPath, enabledPath);
            }
            else if (!enable && File.Exists(enabledPath))
            {
                File.Move(enabledPath, disabledPath);
            }
            
            SetProperty(ref _isAvatarPluginEnabled, enable, nameof(IsAvatarPluginEnabled));
            RefreshUIState();
        }
        catch (Exception ex)
        {
            WeakReferenceMessenger.Default.Send(new NotificationMessage(
                "状态切换失败",
                $"无法修改插件文件后缀名。\n详细信息: {ex.Message}",
                NotificationType.Error,
                6000
            ));
        }
    }

    public void RefreshPluginStates()
    {
        CheckPluginStates();
    }

    private void RefreshUIState()
    {
        OnPropertyChanged(nameof(SettingsOverlayVisibility));
        OnPropertyChanged(nameof(IsSettingsInteractable));
        OnPropertyChanged(nameof(OverlayWarningText));
        OnPropertyChanged(nameof(AvatarSettingsVisibility));
        OnPropertyChanged(nameof(MainSettingsVisibility));
        UpdatePaths();
    }
    
    private void UpdatePaths()
    {
        string subDir = SelectedPluginIndex == 0 ? "FuFuPlugin" : (SelectedPluginIndex == 1 ? "FPS" : "Avatar");
        _pluginDir = Path.Combine(AppContext.BaseDirectory, "Plugins", subDir);
        
        if (SelectedPluginIndex == 2)
        {
            _iniPath = string.Empty;
            string avatarEnabledPath = Path.Combine(_pluginDir, "Avatar.dll");
            string avatarDisabledPath = Path.Combine(_pluginDir, "Avatar.disabled");
            _dllPath = File.Exists(avatarDisabledPath) ? avatarDisabledPath : avatarEnabledPath;
        }
        else
        {
            _iniPath = Path.Combine(_pluginDir, "config.ini");
            if (subDir == "FuFuPlugin")
            {
                string mainEnabledPath = Path.Combine(_pluginDir, "FufuLauncher.UnlockerIsland.dll");
                string mainDisabledPath = Path.Combine(_pluginDir, "FufuLauncher.UnlockerIsland.disabled");
                _dllPath = File.Exists(mainDisabledPath) ? mainDisabledPath : mainEnabledPath;
            }
            else
            {
                string fpsEnabledPath = Path.Combine(_pluginDir, "FPS.dll");
                string fpsDisabledPath = Path.Combine(_pluginDir, "FPS.disabled");
                _dllPath = File.Exists(fpsDisabledPath) ? fpsDisabledPath : fpsEnabledPath;
            }
        }
        
        _presetsDir = Path.Combine(AppPaths.PluginPresetsDir, subDir);
        
        if (!string.IsNullOrEmpty(_iniPath))
        {
            _iniFile = new IniFile(_iniPath);
        }
        else
        {
            _iniFile = null;
        }

        if (!Directory.Exists(_presetsDir))
        {
            try
            {
                Directory.CreateDirectory(_presetsDir);
            }
            catch (UnauthorizedAccessException)
            {
                // If the resolved path is not writable (e.g. under Program Files),
                // fall back to the default AppData-based location.
                _presetsDir = Path.Combine(
                    Path.Combine(AppPaths.RootDir, "Data", "PluginPresets"),
                    Path.GetFileName(_presetsDir));
                Directory.CreateDirectory(_presetsDir);
            }
        }
    }

    public bool IsMainPluginDllMissing()
    {
        string mainDir = Path.Combine(AppContext.BaseDirectory, "Plugins", "FuFuPlugin");
        string mainEnabledPath = Path.Combine(mainDir, "FufuLauncher.UnlockerIsland.dll");
        string mainDisabledPath = Path.Combine(mainDir, "FufuLauncher.UnlockerIsland.disabled");
        return !File.Exists(mainEnabledPath) && !File.Exists(mainDisabledPath);
    }

    public bool IsPluginCorrupted()
    {
        if (File.Exists(_dllPath))
        {
            var fileInfo = new FileInfo(_dllPath);
            return fileInfo.Length < 10 * 1024;
        }
        return false; 
    }
    #endregion
}
