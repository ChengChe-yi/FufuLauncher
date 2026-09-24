/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;

namespace FufuLauncher.Models;

public partial class OfficialBackgroundItem : ObservableObject
{
    public string Url { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;

    public bool IsVideo { get; set; }
    
    public bool IsVideoPoster { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentBadgeVisibility))]
    private bool _isCurrent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BusyVisibility))]
    private bool _isBusy;

    public string TypeText => IsVideo
        ? "OfficialBgWindow_TypeVideo".GetLocalized()
        : IsVideoPoster
            ? "OfficialBgWindow_TypePoster".GetLocalized()
            : "OfficialBgWindow_TypeImage".GetLocalized();

    public Visibility CurrentBadgeVisibility => IsCurrent ? Visibility.Visible : Visibility.Collapsed;

    public Visibility BusyVisibility => IsBusy ? Visibility.Visible : Visibility.Collapsed;
}
