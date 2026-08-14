/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class PluginPage : Page
{
    public PluginViewModel ViewModel
    {
        get;
    }

    public MainViewModel MainViewModel
    {
        get;
    }
    public ControlPanelModel ControlPanelViewModel
    {
        get;
    }

    public PluginPage()
    {
        ViewModel = App.GetService<PluginViewModel>();
        MainViewModel = App.GetService<MainViewModel>();
        ControlPanelViewModel = App.GetService<ControlPanelModel>();

        InitializeComponent();

        ViewModel.DuplicateDetected += ViewModel_DuplicateDetected;
    }
}
