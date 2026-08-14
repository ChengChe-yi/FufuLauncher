/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using Microsoft.UI.Xaml.Media;

namespace FufuLauncher.Views;

public sealed partial class GameAnnouncementWindow : WindowEx
{
    public GameAnnouncementWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        ContentFrame.Navigate(typeof(GameAnnouncementListPage));

        this.CenterOnScreen();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        IsShownInSwitchers = true;
    }
}
