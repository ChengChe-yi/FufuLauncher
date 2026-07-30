/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using FufuLauncher.Services.Backpack;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;

namespace FufuLauncher.ViewModels;

public sealed class IngredientViewModel : ObservableObject
{
    public string Name { get; }
    public string HeldText { get; }
    public Uri? IconUri { get; }
    public Visibility EnoughVisibility { get; }
    public Visibility ShortVisibility { get; }
    public bool IsEnough { get; }

    public IngredientViewModel(FoodMetaService.IngredientMeta meta, ulong held, Uri? iconUri)
    {
        Name = meta.Name;
        HeldText = $"{held}/{meta.Amount}";
        IconUri = iconUri;
        var enough = held >= (ulong)meta.Amount;
        IsEnough = enough;
        EnoughVisibility = enough.ToVisibility();
        ShortVisibility = (!enough).ToVisibility();
    }
}
