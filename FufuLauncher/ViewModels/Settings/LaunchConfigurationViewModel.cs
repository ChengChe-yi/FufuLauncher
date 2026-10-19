using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Services;
using Microsoft.UI.Dispatching;

namespace FufuLauncher.ViewModels;

public partial class LaunchConfigurationViewModel : ObservableObject
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IGameLauncherService _gameLauncherService;
    private readonly AccountManager _accountManager;
    private readonly Services.AuthTicket.IAuthTicketService _authTicketService;
    private readonly DispatcherQueue _dispatcherQueue;
    private bool _isInitializing = true;
    private bool _isLoadingLaunchParams;
    private Task? _loadTask;

    public record MonitorItem(string DisplayName, int Index);

    public record PostLaunchBehaviorItem(string DisplayName, PostLaunchBehavior Value);

    public record InjectionModuleItem(string DisplayName, string Id);

    [ObservableProperty] private bool _isCustomLaunchParametersEnabled;
    [ObservableProperty] private string _customLaunchParameters = "";
    [ObservableProperty] private WindowModeType _launchArgsWindowMode = WindowModeType.Normal;
    [ObservableProperty] private string _launchArgsWidth = "";
    [ObservableProperty] private string _launchArgsHeight = "";
    [ObservableProperty] private string _customGameExeName = "";
    [ObservableProperty] private ObservableCollection<MonitorItem> _availableMonitors = new();
    [ObservableProperty] private MonitorItem? _selectedMonitor;
    [ObservableProperty] private int _launchArgsMonitorIndex;
    [ObservableProperty] private PostLaunchBehavior _postLaunchBehavior;
    [ObservableProperty] private PostLaunchBehaviorItem? _selectedPostLaunchBehaviorItem;
    [ObservableProperty] private InjectionModuleItem? _selectedInjectionModule;
    [ObservableProperty] private bool _isPluginConflictCheckEnabled = true;
    [ObservableProperty] private bool _isPluginConflictMainDllOnly = true;
    [ObservableProperty] private bool _isUsingHoyolabAccount;
    [ObservableProperty] private bool _isRedeemCodeNotificationEnabled = true;
    [ObservableProperty] private bool _isReady;

    public List<PostLaunchBehaviorItem> PostLaunchBehaviorItems
    {
        get;
    } = new()
    {
        new("LaunchConfig_PostLaunch_None".GetLocalized(), Models.PostLaunchBehavior.None),
        new("LaunchConfig_PostLaunch_MinimizeToTray".GetLocalized(), Models.PostLaunchBehavior.MinimizeToTray),
        new("LaunchConfig_PostLaunch_Exit".GetLocalized(), Models.PostLaunchBehavior.Exit)
    };

    public List<InjectionModuleItem> InjectionModules
    {
        get;
    } = new()
    {
        new("InjectionBuiltIn".GetLocalized(), "DLL"),
        new("InjectionStandalone".GetLocalized(), "EXE")
    };

    public IRelayCommand<string> SetResolutionPresetCommand
    {
        get;
    }

    public IAsyncRelayCommand ResetGameExeNameCommand
    {
        get;
    }

    public LaunchConfigurationViewModel(ILocalSettingsService localSettingsService,
        IGameLauncherService gameLauncherService, AccountManager accountManager,
        Services.AuthTicket.IAuthTicketService authTicketService)
    {
        _localSettingsService = localSettingsService;
        _gameLauncherService = gameLauncherService;
        _accountManager = accountManager;
        _authTicketService = authTicketService;
        _dispatcherQueue = App.MainWindow.DispatcherQueue;
        ResetGameExeNameCommand = new AsyncRelayCommand(ResetGameExeNameAsync);
        SetResolutionPresetCommand = new RelayCommand<string>(value =>
        {
            var parts = value?.Split(' ');
            if (parts is not { Length: 2 }) return;
            LaunchArgsWidth = parts[0];
            LaunchArgsHeight = parts[1];
        });
    }

    public Task LoadAsync()
    {
        if (_loadTask is { IsCompleted: false }) return _loadTask;
        return _loadTask = LoadCoreAsync();
    }

    private async Task LoadCoreAsync()
    {
        IsReady = false;
        _isInitializing = true;
        _isLoadingLaunchParams = true;
        try
        {
            var isCustomParamsEnabled = await _localSettingsService.ReadSettingAsync("IsCustomLaunchParametersEnabled");
            IsCustomLaunchParametersEnabled = isCustomParamsEnabled != null && Convert.ToBoolean(isCustomParamsEnabled);

            var parameters = await _localSettingsService.ReadSettingAsync("CustomLaunchParameters");
            InitializeDefaultResolution();
            CustomLaunchParameters = parameters?.ToString() ?? "";
            LaunchArgsWindowMode = WindowModeType.Normal;
            LaunchArgsMonitorIndex = 0;
            ParseLaunchParameters(CustomLaunchParameters);

            var customExe = await _localSettingsService.ReadSettingAsync(GameExeManager.CustomExeNameKey);
            CustomGameExeName = customExe?.ToString() ?? "";

            var behavior = await _localSettingsService.ReadSettingAsync("PostLaunchBehavior");
            PostLaunchBehavior = Enum.TryParse<PostLaunchBehavior>(behavior?.ToString(), out var parsed)
                                 && PostLaunchBehaviorItems.Any(item => item.Value == parsed)
                ? parsed
                : Models.PostLaunchBehavior.None;
            SelectedPostLaunchBehaviorItem = PostLaunchBehaviorItems.First(item => item.Value == PostLaunchBehavior);

            var module = await _localSettingsService.ReadSettingAsync("InjectionModule");
            SelectedInjectionModule = InjectionModules.FirstOrDefault(item => item.Id == module?.ToString())
                                      ?? InjectionModules[0];

            var hoyolab = await _localSettingsService.ReadSettingAsync("UsingHoyolabAccount");
            IsUsingHoyolabAccount = hoyolab != null && Convert.ToBoolean(hoyolab);
            var redeem = await _localSettingsService.ReadSettingAsync("IsRedeemCodeNotificationEnabled");
            IsRedeemCodeNotificationEnabled = redeem == null || Convert.ToBoolean(redeem);
            var conflict = await _localSettingsService.ReadSettingAsync(PluginConflictSettings.CheckEnabledKey);
            IsPluginConflictCheckEnabled = conflict == null || Convert.ToBoolean(conflict);
            var mainOnly = await _localSettingsService.ReadSettingAsync(PluginConflictSettings.MainDllOnlyKey);
            IsPluginConflictMainDllOnly = mainOnly == null || Convert.ToBoolean(mainOnly);
            LoadMonitors();
            IsReady = true;
        }
        finally
        {
            _isLoadingLaunchParams = false;
            _isInitializing = false;
        }
    }

    partial void OnSelectedInjectionModuleChanged(InjectionModuleItem? value)
    {
        if (_isInitializing || value == null) return;
        _ = _localSettingsService.SaveSettingAsync("InjectionModule", value.Id);
    }

    partial void OnIsPluginConflictCheckEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync(PluginConflictSettings.CheckEnabledKey, value);
    }

    partial void OnIsPluginConflictMainDllOnlyChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync(PluginConflictSettings.MainDllOnlyKey, value);
    }

    partial void OnIsRedeemCodeNotificationEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsRedeemCodeNotificationEnabled", value);
    }

    partial void OnIsCustomLaunchParametersEnabledChanged(bool value)
    {
        if (_isInitializing) return;
        _ = _localSettingsService.SaveSettingAsync("IsCustomLaunchParametersEnabled", value);
    }
}