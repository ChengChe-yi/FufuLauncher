/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Helpers;
using FufuLauncher.Models;

namespace FufuLauncher.ViewModels;

public partial class MainViewModel
{
    #region 注入模块
    [ObservableProperty] private bool _useInjection;

    [ObservableProperty] private string _injectionModule = "DLL";
    [ObservableProperty] private ObservableCollection<InjectionModuleInfo> _availableInjectionModules = new();
    public IRelayCommand<InjectionModuleInfo> SelectInjectionModuleCommand { get; }

    partial void OnUseInjectionChanged(bool value)
    {
        _ = Task.Run(async () =>
        {
            await _gameLauncherService.SetUseInjectionAsync(value);
            var actual = await _gameLauncherService.GetUseInjectionAsync();
            if (actual != value)
            {
                await UpdateUI(() => UseInjection = actual);
            }

            await UpdateUI(() => UpdateLaunchButtonState());
        });
    }

    private void InitializeInjectionModules()
    {
        AvailableInjectionModules = new ObservableCollection<InjectionModuleInfo>
        {
            new() { Id = "DLL", Name = "InjectionBuiltIn".GetLocalized(), Description = "InjectionBuiltInDesc".GetLocalized(), IsSelected = true },
            new() { Id = "EXE", Name = "InjectionStandalone".GetLocalized(), Description = "InjectionStandaloneDesc".GetLocalized(), IsSelected = false }
        };
    }

    private void SelectInjectionModule(InjectionModuleInfo module)
    {
        if (module == null) return;

        InjectionModule = module.Id;

        foreach (var m in AvailableInjectionModules)
        {
            m.IsSelected = m.Id == module.Id;
        }

        _ = _localSettingsService.SaveSettingAsync("InjectionModule", module.Id);
    }

    private async Task LoadInjectionModuleAsync()
    {
        try
        {
            var saved = await _localSettingsService.ReadSettingAsync("InjectionModule");
            var moduleId = saved?.ToString() ?? "DLL";
            InjectionModule = moduleId;

            foreach (var m in AvailableInjectionModules)
            {
                m.IsSelected = m.Id == moduleId;
            }
        }
        catch
        {
            // ignored
        }
    }
    #endregion
}
