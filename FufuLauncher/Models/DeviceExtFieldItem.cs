/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace FufuLauncher.Models;

public partial class DeviceExtFieldItem : ObservableObject
{
    [ObservableProperty] private string _key = "";
    [ObservableProperty] private string _value = "";

    public JsonValueKind Kind
    {
        get;
        set;
    } = JsonValueKind.String;
}