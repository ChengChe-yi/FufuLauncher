/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Helpers;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public partial class PluginSettingsViewModel : ObservableObject
{
    private string _iniPath;
    private string _pluginDir;
    private string _presetsDir;
    private string _dllPath;
    private IniFile _iniFile;
    private bool _useKeyListInput = true;
    private readonly LightweightPluginService _lightweightPlugin;
    
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDownloadSupported))]
    [NotifyPropertyChangedFor(nameof(ModeTabVisibility))]
    [NotifyPropertyChangedFor(nameof(SelectedPluginComboLabel))]
    private int selectedPluginIndex = 0;
    
    public bool IsDownloadSupported => SelectedPluginIndex == 0 && !IsLightweightMode;

    public bool IsLightweightMode => _lightweightPlugin.IsLightweightMode;

    public bool IsStandardModeTab => !IsLightweightMode;

    public bool IsLightweightModeTab => IsLightweightMode;

    public bool IsLitePluginInstallSupported => IsLightweightMode;

    public Microsoft.UI.Xaml.Visibility ModeTabVisibility =>
        SelectedPluginIndex == 0 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public string SelectedPluginComboLabel =>
        IsLightweightMode ? "LightweightMode_PluginComboLabel".GetLocalized() : "PluginFuFuMain".GetLocalized();

    public string PluginToggleLabel =>
        IsLightweightMode ? "LightweightMode_LiteEnabledLabel".GetLocalized() : "MainPluginEnabledLabel".GetLocalized();

    [ObservableProperty]
    private string pluginName;

    [ObservableProperty]
    private string pluginDescription;

    [ObservableProperty]
    private string pluginDeveloper;

    [ObservableProperty]
    private string lastModifiedDate;

    [ObservableProperty]
    private ObservableCollection<PresetModel> availablePresets = new();

    [ObservableProperty]
    private PresetModel currentPreset;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource currentAvatarSource;

    [ObservableProperty]
    private bool hasAvatar;
    
    
    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource avatar512Source;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource avatar256Source;

    [ObservableProperty]
    private Microsoft.UI.Xaml.Media.ImageSource avatar128Source;

    [ObservableProperty]
    private bool hasAvatar512;

    [ObservableProperty]
    private bool hasAvatar256;

    [ObservableProperty]
    private bool hasAvatar128;

    
    
    private bool _isAutoCreatePresetEnabled = false;

    public bool IsAutoCreatePresetEnabled
    {
        get => _isAutoCreatePresetEnabled;
        set
        {
            if (SetProperty(ref _isAutoCreatePresetEnabled, value))
            {
                var localSettings = App.GetService<FufuLauncher.Contracts.Services.ILocalSettingsService>();
                if (localSettings != null)
                {
                    _ = localSettings.SaveSettingAsync("IsAutoCreatePresetEnabled", value);
                }
            }
        }
    }
    
    public ObservableCollection<PluginSettingItem> Settings { get; } = new();

    public Microsoft.UI.Xaml.Visibility AvatarSettingsVisibility => 
        SelectedPluginIndex == 2 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public Microsoft.UI.Xaml.Visibility MainSettingsVisibility => 
        SelectedPluginIndex != 2 ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;


    partial void OnSelectedPluginIndexChanged(int value)
    {
        CheckPluginStates();
        UpdatePaths();
        LoadConfiguration();
        UpdateAvatarPreview();
        RefreshUIState();
    }
    

    public PluginSettingsViewModel()
    {
        _lightweightPlugin = App.GetService<LightweightPluginService>();
        CheckPluginStates();
        UpdatePaths();
        _pluginDir = GetMainPluginDirectory();
        _iniPath = IsLightweightMode
            ? LightweightPluginService.LitePluginConfigPath
            : Path.Combine(_pluginDir, "config.ini");
        _dllPath = IsLightweightMode
            ? LightweightPluginService.FindLitePluginDisabledPath() ?? LightweightPluginService.LitePluginDllPath
            : LightweightPluginService.MainPluginDllPath;
        _presetsDir = IsLightweightMode
            ? Path.Combine(AppPaths.PluginPresetsDir, LightweightPluginService.LitePluginFolderName)
            : AppPaths.PluginPresetsDir;
    
        _iniFile = new IniFile(_iniPath);
    
        try
        {
            if (!Directory.Exists(_presetsDir))
            {
                Directory.CreateDirectory(_presetsDir);
            }
        }
        catch (UnauthorizedAccessException)
        {
            _presetsDir = Path.Combine(AppPaths.RootDir, "Data", "PluginPresets");
            try { Directory.CreateDirectory(_presetsDir); }
            catch (Exception inner) { System.Diagnostics.Debug.WriteLine($"目录创建失败: {inner.Message}"); }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"目录创建失败: {ex.Message}");
        }
        
        var localSettings = App.GetService<FufuLauncher.Contracts.Services.ILocalSettingsService>();
        if (localSettings != null)
        {
            var keyInputTask = localSettings.ReadSettingAsync("UseKeyListInput");
            keyInputTask.Wait();
            _useKeyListInput = keyInputTask.Result == null || Convert.ToBoolean(keyInputTask.Result);
            
            var autoCreateTask = localSettings.ReadSettingAsync("IsAutoCreatePresetEnabled");
            autoCreateTask.Wait();
            _isAutoCreatePresetEnabled = autoCreateTask.Result != null && Convert.ToBoolean(autoCreateTask.Result);
            
            var devFeaturesTask = localSettings.ReadSettingAsync("IsDevFeaturesEnabled");
            devFeaturesTask.Wait();
            bool savedDevFeatures = devFeaturesTask.Result != null && Convert.ToBoolean(devFeaturesTask.Result);

            if (savedDevFeatures)
            {
                _isDevFeaturesEnabled = true;
                _ = VerifyAndApplyDevFeaturesOnStartupAsync();
            }
            else
            {
                _isDevFeaturesEnabled = false;
            }
        }
        
        LoadConfiguration();
        UpdateAvatarPreview();
    }
    

    public bool UseKeyListInput
    {
        get => _useKeyListInput;
        set
        {
            if (SetProperty(ref _useKeyListInput, value))
            {
                foreach (var setting in Settings.Where(s => string.Equals(s.Type, "key", StringComparison.OrdinalIgnoreCase)))
                {
                    setting.SetKeyInputMode(value);
                }

                var localSettings = App.GetService<FufuLauncher.Contracts.Services.ILocalSettingsService>();
                if (localSettings != null)
                {
                    _ = localSettings.SaveSettingAsync("UseKeyListInput", value);
                }
            }
        }
    }

    
    private bool _isDevFeaturesEnabled;
    public bool IsDevFeaturesEnabled
    {
        get => _isDevFeaturesEnabled;
        set
        {
            if (_isDevFeaturesEnabled != value)
            {
                _isDevFeaturesEnabled = value;
                OnPropertyChanged();

                if (value)
                {
                    _ = VerifyAndApplyDevFeaturesAsync();
                }
                else
                {
                    SaveDevFeaturesSetting(false);
                    LoadConfiguration();
                }
            }
        }
    }

    public void RefreshModeState()
    {
        OnPropertyChanged(nameof(IsLightweightMode));
        OnPropertyChanged(nameof(IsStandardModeTab));
        OnPropertyChanged(nameof(IsLightweightModeTab));
        OnPropertyChanged(nameof(IsDownloadSupported));
        OnPropertyChanged(nameof(IsLitePluginInstallSupported));
        OnPropertyChanged(nameof(PluginToggleLabel));
        OnPropertyChanged(nameof(SelectedPluginComboLabel));

        CheckPluginStates();
        UpdatePaths();
        LoadConfiguration();
        UpdateAvatarPreview();
        RefreshUIState();
    }

}
