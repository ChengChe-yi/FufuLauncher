using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;

namespace FufuLauncher.Views;

public sealed partial class CycleChallengeWindow : Window
{
    public CycleChallengeWindow()
    {
        InitializeComponent();

        RootGrid.RequestedTheme = App.GetService<IThemeSelectorService>().Theme;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        WindowManagerHelper.ResizeWithDpi(AppWindow, this, 1120, 760);
        WindowManagerHelper.CenterWindowOnScreen(AppWindow, 1120, 760);

        ContentFrame.Navigate(typeof(CycleChallengePage));
    }
}