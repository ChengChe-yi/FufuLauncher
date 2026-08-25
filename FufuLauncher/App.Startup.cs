/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Services;
using FufuLauncher.Services.Background;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml;

namespace FufuLauncher;

public partial class App
{
    #region Startup

    protected async override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            base.OnLaunched(args);
            Debug.WriteLine("App启动开始");

            _mainDispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            _ = Task.Run(LoadUidLookupAsync);

            await VerifyResourceFilesAsync();

            if (!AppPaths.IsFirstRun)
            {
                await ApplyLanguageSettingAsync();
                await SetDefaultThemeAsync();
            }
            else
            {
                await ApplyLanguageSettingAsync();

                WeakReferenceMessenger.Default.Register<Messages.AgreementAcceptedMessage>(this, (r, m) =>
                {
                    WeakReferenceMessenger.Default.Unregister<Messages.AgreementAcceptedMessage>(r);
                    _mainDispatcherQueue.TryEnqueue(async () =>
                    {
                        await SetDefaultThemeAsync();
                    });
                });
            }
            var accountManager = GetService<AccountManager>();
            await accountManager.InitializeAsync();
            MainWindow = new MainWindow();
            _cpuUsageMonitor = new ProcessCpuUsageMonitor(_mainDispatcherQueue, GetService<ILocalSettingsService>());
            _cpuUsageMonitor.Start();
            if (MainWindow is MainWindow mainWindow)
            {
                await mainWindow.InitializeWindowSizeAsync();
            }

            var activationService = GetService<IActivationService>();
            await activationService.ActivateAsync(args);

            Debug.WriteLine("App主窗口已激活");
            var shouldRunBackgroundTasks = !AppPaths.IsFirstRun && 
                !(MainWindow is MainWindow mw && mw.IsAgreementShowing);
            if (shouldRunBackgroundTasks)
            {
                _ = Task.Run(PlayStartupSoundDelayedAsync);

                _ = Task.Run(CheckForAnnouncementAsync);

                _ = Task.Run(RunStartupUpdateCheckAsync);

                ProcessStartupLaunchArguments();
            }

            Debug.WriteLine("App启动完成");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"启动失败: {ex.Message}");
            LogException(ex, "OnLaunched (启动流程异常)");
            ShowCrashDialog("应用启动流程异常", ex);
            Environment.Exit(-1);
        }
    }

    private async Task SetDefaultThemeAsync()
    {
        try
        {
            var localSettings = GetService<ILocalSettingsService>();
            var isThemeInitialized = await localSettings.ReadSettingAsync("IsThemeInitialized");

            if (isThemeInitialized == null)
            {
                Debug.WriteLine("Initializing default theme to Dark.");
                var themeService = GetService<IThemeSelectorService>();

                await themeService.SetThemeAsync(ElementTheme.Dark);

                await localSettings.SaveSettingAsync("IsThemeInitialized", true);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Failed to set default theme: {ex.Message}");
        }
    }

    private async Task VerifyResourceFilesAsync()
    {
        try
        {
            var resourceManager = new Microsoft.Windows.ApplicationModel.Resources.ResourceManager();

            var resourceMap = resourceManager.MainResourceMap;

            var resourceCandidate = resourceMap.GetValue("Resources/AppDisplayName");

            if (resourceCandidate != null)
            {
                var test = resourceCandidate.ValueAsString;
                Debug.WriteLine($"资源加载成功: {test}");
            }
            else
            {
                Debug.WriteLine("警告: 找不到资源 AppDisplayName");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"资源加载严重失败: {ex.Message}");
        }
    }

    private async Task ApplyLanguageSettingAsync()
    {
        try
        {
            var localSettingsService = GetService<ILocalSettingsService>();
            var languageValue = await localSettingsService.ReadSettingAsync("AppLanguage");

            Debug.WriteLine($"[App] ApplyLanguageSettingAsync: raw value='{languageValue}' (type={languageValue?.GetType().Name ?? "null"})");

            var language = languageValue != null && int.TryParse(languageValue.ToString(), out var languageCode)
                ? (AppLanguage)languageCode
                : AppLanguage.Default;
            var culture = LanguagePreferenceResolver.Resolve(
                language,
                Windows.System.UserProfile.GlobalizationPreferences.Languages);

            Debug.WriteLine($"[App] ApplyLanguageSettingAsync: language={language}, culture='{culture}'");
            ResourceExtensions.SetLanguage(culture);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] ApplyLanguageSettingAsync ERROR: {ex.Message}");
        }
    }

    private void ApplyLanguageSetting()
    {
        try
        {
            var localSettingsService = GetService<ILocalSettingsService>();
            var languageValue = localSettingsService.ReadSettingAsync("AppLanguage").Result;

            if (languageValue != null)
            {
                if (!int.TryParse(languageValue.ToString(), out var languageCode))
                    return;

                var culture = LanguagePreferenceResolver.Resolve(
                    (AppLanguage)languageCode,
                    Windows.System.UserProfile.GlobalizationPreferences.Languages);
                Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = culture;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"应用语言设置失败: {ex.Message}");
        }
    }

    #endregion
}
