using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.UI;

namespace FufuLauncher.Views;

public sealed partial class ServiceStatusWindow : Window
{
    private static ServiceStatusWindow? _current;

    public ServiceStatusViewModel ViewModel
    {
        get;
    }

    public static void ShowOrActivate()
    {
        if (_current != null)
        {
            _current.Activate();
            return;
        }

        var window = new ServiceStatusWindow();
        window.Closed += (_, _) => _current = null;

        _current = window;
        window.Activate();
    }

    public ServiceStatusWindow()
    {
        ViewModel = new ServiceStatusViewModel();

        InitializeComponent();

        Title = "ServiceStatus_WindowTitle".GetLocalized();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        Closed += OnClosed;

        ConfigureWindow();
        ApplyThemeAndChrome();

        try
        {
            SystemBackdrop = new MicaBackdrop();
        }
        catch
        {
            try
            {
                SystemBackdrop = new DesktopAcrylicBackdrop();
            }
            catch
            {
                var isDark = IsDarkTheme();
                RootGrid.Background = new SolidColorBrush(isDark
                    ? Color.FromArgb(255, 32, 32, 32)
                    : Color.FromArgb(255, 243, 243, 243));
            }
        }

        ViewModel.RefreshCommand.Execute(null);
    }

    private void ConfigureWindow()
    {
        try
        {
            var appWindow = AppWindow;
            if (appWindow == null)
            {
                return;
            }

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "WindowIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            var scale = WindowManagerHelper.GetScaleFactor(this);
            var minWidth = (int)Math.Round(480 * scale);
            var minHeight = (int)Math.Round(400 * scale);
            var width = (int)Math.Round(1168 * scale);
            var height = (int)Math.Round(720 * scale);

            var displayArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary);
            if (displayArea != null)
            {
                width = Math.Min(width, Math.Max(minWidth, displayArea.WorkArea.Width - 24));
                height = Math.Min(height, Math.Max(minHeight, displayArea.WorkArea.Height - 24));
            }

            var size = new SizeInt32(width, height);
            appWindow.Resize(size);
            WindowManagerHelper.CenterWindowOnScreen(appWindow, size.Width, size.Height);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.PreferredMinimumWidth = minWidth;
                presenter.PreferredMinimumHeight = minHeight;
            }
        }
        catch
        {
        }
    }

    private void ApplyThemeAndChrome()
    {
        try
        {
            var theme = App.GetService<IThemeSelectorService>().Theme;
            RootGrid.RequestedTheme = theme;

            var isDark = IsDarkTheme();

            var titleBar = AppWindow?.TitleBar;
            if (titleBar == null)
            {
                return;
            }

            titleBar.BackgroundColor = Colors.Transparent;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonInactiveForegroundColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonHoverForegroundColor = isDark ? Colors.White : Colors.Black;
            titleBar.ButtonHoverBackgroundColor = isDark
                ? Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x33, 0x00, 0x00, 0x00);
            titleBar.ButtonPressedBackgroundColor = isDark
                ? Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)
                : Color.FromArgb(0x66, 0x00, 0x00, 0x00);
        }
        catch
        {
        }
    }

    private bool IsDarkTheme()
    {
        var requested = RootGrid.RequestedTheme;
        return requested == ElementTheme.Dark ||
               (requested == ElementTheme.Default && Application.Current.RequestedTheme == ApplicationTheme.Dark);
    }

    private void OnOpenDetailClick(object sender, RoutedEventArgs e)
    {
        OpenDetail(sender);
    }

    private static void OpenDetail(object sender)
    {
        if (sender is FrameworkElement element)
        {
            var monitor = element.Tag as ServiceStatusMonitor ??
                          element.DataContext as ServiceStatusMonitor;
            if (monitor != null)
            {
                ServiceMonitorDetailWindow.ShowOrActivate(monitor.MonitorId, monitor.Name);
            }
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        ViewModel.Cancel();
    }
}