/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System;
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Web.WebView2.Core;

namespace FufuLauncher.Views;

public sealed partial class UpdateNotificationWindow : WindowEx
{
    public UpdateNotificationWindow(string updateInfoUrl)
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        UpdateWebView.NavigationStarting += UpdateWebView_NavigationStarting;
        UpdateWebView.Source = new Uri(updateInfoUrl);

        this.CenterOnScreen();
        SystemBackdrop = new DesktopAcrylicBackdrop();
        IsShownInSwitchers = true;
    }

    private void UpdateWebView_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
    {
        try
        {
            sender.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[UpdateNotificationWindow] {ex.Message}");
        }
    }
    
    private async void OnUpdateBtnClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            UpdateWebView?.Close();
        }
        catch { }
        
        await Task.Delay(200);

        if (App.MainWindow is MainWindow mainWindow)
        {
            await mainWindow.NavigateToSettingsUpdateSectionAsync();
        }

        Close();
    }
}
