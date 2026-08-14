/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Services.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using QuestPDF.Infrastructure;

namespace FufuLauncher;

public partial class App : Application
{
    #region Core

    public IHost Host
    {
        get;
    }

    public static T GetService<T>()
        where T : class
    {
        if ((App.Current as App)!.Host.Services.GetService(typeof(T)) is not T service)
        {
            throw new ArgumentException($"{typeof(T)} needs to be registered in ConfigureServices within App.xaml.cs.");
        }

        return service;
    }

    public static WindowEx MainWindow { get; private set; }

    /// <summary>
    /// Tracks the language selected on the first-run language selection page,
    /// so the AgreementPage can show the appropriate agreement text.
    /// </summary>
    public static ViewModels.AppLanguage? FirstRunSelectedLanguage { get; set; }

    public static UIElement? AppTitlebar
    {
        get; set;
    }
    private static Microsoft.UI.Dispatching.DispatcherQueue _mainDispatcherQueue = null!;
    private ProcessCpuUsageMonitor? _cpuUsageMonitor;

    public App()
    {
        Helpers.AppPaths.EnsureDirectories();
        QuestPDF.Settings.License = LicenseType.Community;

        string userDataFolder = Path.Combine(Helpers.AppPaths.CacheDir, "WebView2Data");
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", userDataFolder);

        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        UnhandledException += App_UnhandledException;

        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        InitializeComponent();

        // 清理旧版遗留的 resources.pri
        try
        {
            string oldPri = Path.Combine(AppContext.BaseDirectory, "resources.pri");
            if (File.Exists(oldPri))
            {
                File.Delete(oldPri);
                Debug.WriteLine("[App] 已删除残留的 resources.pri");
            }
        }
        catch { }

        var appInstance = AppInstance.GetCurrent();
        appInstance.Activated += App_Activated!;

        try
        {
            Host = CreateHost();

            CleanupOldSettings();

            Services.GameServer.GameServerCacheMaintenance.CleanLegacyCaches();
        }
        catch (Exception ex)
        {
            LogException(ex, "App Constructor");
            ShowCrashDialog("核心配置及服务初始化异常", ex);
            Environment.Exit(-1);
        }
    }

    private void App_Activated(object sender, AppActivationArguments e)
    {
        _mainDispatcherQueue?.TryEnqueue(() =>
        {
            MainWindow.Activate();
        });
    }

    private void CleanupOldSettings()
    {
        try
        {
            var filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FufuLauncher", "ApplicationData", "LocalSettings.json"
            );

            if (File.Exists(filePath))
            {
                var content = File.ReadAllText(filePath);
                if (content.Contains("System.Private.CoreLib") || content.Contains("True") || content.Contains("False"))
                {
                    File.Delete(filePath);
                    Debug.WriteLine("清理了旧的无效设置文件");
                }
            }
        }
        catch
        {
            // ignored
        }
    }

    #endregion
}
