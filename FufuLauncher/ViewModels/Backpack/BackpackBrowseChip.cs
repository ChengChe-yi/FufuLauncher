/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.ComponentModel;

namespace FufuLauncher.ViewModels;

public sealed partial class BackpackBrowseChip : ObservableObject
{
    public string Key { get; }
    public string Label { get; }
    public string Glyph { get; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public BackpackBrowseChip(string key, string label, string glyph = "", bool isSelected = false)
    {
        Key = key;
        Label = label;
        Glyph = glyph;
        IsSelected = isSelected;
    }
}
