using FufuLauncher.Helpers;
using FufuLauncher.Models.Backpack;
using FufuLauncher.Services.Backpack;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.ViewModels;

public sealed partial class WeaponViewModel : ObservableObject
{
    private static readonly int[] MaxLevelByPromote = [20, 40, 50, 60, 70, 80, 90];

    public WeaponEntry  Source                { get; }
    public string       RankDisplay           { get; }
    public string       Level                 { get; }
    public string       LevelFull             { get; }
    public string       RefineLabel           { get; }
    public string       Refine                { get; }
    public string       AtkDisplay            { get; }
    public string       SubDisplay            { get; }
    public string       TypeRankDisplay       { get; }
    public string       PassiveName           { get; }
    public string       SkillDesc             { get; }
    public string       FlavorText            { get; }
    public Visibility   HasInstanceVisibility { get; }
    public Visibility   SubVisibility         { get; }
    public Visibility   PassiveVisibility     { get; }
    public Visibility   FlavorVisibility      { get; }
    public bool         HasInstance           { get; }
    public Uri?         IconUri               { get; }
    public BitmapImage  QualitySource         { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandedVisibility))]
    public partial bool IsExpanded { get; set; }

    public Visibility ExpandedVisibility => IsExpanded.ToVisibility();

    public WeaponViewModel(WeaponEntry entry, WeaponMetaService meta)
    {
        Source      = entry;
        RankDisplay = new string('★', Math.Clamp(entry.Rank, 0, 5));

        var hasInstance = !string.IsNullOrEmpty(entry.Guid);
        HasInstance = hasInstance;
        HasInstanceVisibility = hasInstance.ToVisibility();
        TypeRankDisplay       = $"{entry.Type}  {RankDisplay}";

        if (hasInstance)
        {
            Level       = $"Lv.{entry.Level}";
            LevelFull   = $"Lv.{entry.Level}/{MaxLevelByPromote[Math.Clamp(entry.Promote, 0, 6)]}";
            RefineLabel = $"精炼{entry.Refine}阶";
            Refine      = $"R{entry.Refine}";
            var (atk, sub)     = meta.CalcStats(entry.Id, entry.Level, entry.Promote);
            var (pName, pDesc) = meta.GetSkill(entry.Id, entry.Refine);
            AtkDisplay         = atk > 0 ? atk.ToString() : string.Empty;
            SubDisplay         = sub;
            PassiveName        = pName;
            SkillDesc          = pDesc;
            FlavorText         = meta.GetFlavorText(entry.Id);
        }
        else
        {
            Level = Refine = LevelFull = RefineLabel = AtkDisplay = SubDisplay = string.Empty;
            PassiveName = meta.GetSkill(entry.Id, 1).Name;
            FlavorText  = meta.GetFlavorText(entry.Id);
            SkillDesc   = string.Empty;
        }

        SubVisibility     = (hasInstance && !string.IsNullOrEmpty(SubDisplay)).ToVisibility();
        PassiveVisibility = (!string.IsNullOrEmpty(PassiveName)).ToVisibility();
        FlavorVisibility  = (!string.IsNullOrEmpty(FlavorText)).ToVisibility();

        IconUri = meta.GetIcon(entry.Id);
        QualitySource = StaticResources.QualityImage(entry.Rank);
    }
}
