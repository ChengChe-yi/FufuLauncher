/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using FufuLauncher.Services.Backpack;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.UI;

namespace FufuLauncher.ViewModels;

public sealed class FoodViewModel : ObservableObject
{
    public string         Name                { get; }
    public string         Count               { get; }
    public ulong          CountValue          { get; }
    public int            Rank                { get; }
    public bool           IsCookable          { get; }
    public string         Character           { get; }
    public string         IngredientsText     { get; }
    public string         VariantLabel        { get; }
    public SolidColorBrush VariantForeground  { get; }
    public Uri            IconUri             { get; }
    public BitmapImage    QualitySource       { get; }
    public Visibility     CharacterVisibility { get; }
    public IReadOnlyList<IngredientViewModel> Ingredients { get; }

    public FoodViewModel(FoodMetaService.FoodMeta meta, ulong count, IReadOnlyList<IngredientViewModel> ingredients)
    {
        Name                = meta.Name;
        Count               = count.ToString("N0");
        CountValue          = count;
        Rank                = meta.Rank;
        IsCookable          = ingredients.All(ingredient => ingredient.IsEnough);
        Character           = meta.Character;
        CharacterVisibility = (!string.IsNullOrEmpty(meta.Character)).ToVisibility();

        VariantLabel = meta.Variant switch
        {
            "suspicious" => "奇怪",
            "delicious"  => "美味",
            "special"    => "特殊",
            "sweet"      => "糖雕",
            _            => "普通",
        };

        VariantForeground = new SolidColorBrush(meta.Variant switch
        {
            "suspicious" => Color.FromArgb(255, 157, 105, 213),
            "delicious"  => Color.FromArgb(255, 193, 148,  48),
            "special"    => Color.FromArgb(255,  56, 165,  90),
            "sweet"      => Color.FromArgb(255, 214,  89, 151),
            _            => Color.FromArgb(160, 140, 140, 140),
        });

        IconUri         = StaticResources.MaterialIcon(meta.Icon);
        QualitySource   = StaticResources.QualityImage(meta.Rank);
        Ingredients     = ingredients;
        IngredientsText = string.Join("  ", meta.Ingredients.Select(i => $"{i.Name} ×{i.Amount}"));
    }
}
