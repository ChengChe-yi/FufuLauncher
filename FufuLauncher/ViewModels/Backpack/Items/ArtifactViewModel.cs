using FufuLauncher.Helpers;
using FufuLauncher.Models.Backpack;
using FufuLauncher.Services.Backpack;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.ViewModels;

public sealed partial class ArtifactViewModel : ObservableObject
{
    public ArtifactEntry                       Source                { get; }
    public string                              RankDisplay           { get; }
    public string                              Level                 { get; }
    public string                              SlotRankEquipped      { get; }
    public string                              MainStatValueDisplay  { get; }
    public string                              BonusText             { get; }
    public IReadOnlyList<SubStatItemViewModel> SubStatItems          { get; }
    public Visibility                          HasInstanceVisibility { get; }
    public Visibility                          HasAnyBonusVisibility { get; }
    public bool                                HasInstance           { get; }
    public Uri?                                IconUri               { get; }
    public BitmapImage                         QualitySource         { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExpandedVisibility))]
    public partial bool IsExpanded { get; set; }

    public Visibility ExpandedVisibility => IsExpanded.ToVisibility();

    public ArtifactViewModel(ArtifactEntry entry, ArtifactMetaService meta)
    {
        Source      = entry;
        RankDisplay = new string('★', Math.Clamp(entry.Rank, 0, 5));

        var hasInstance = !string.IsNullOrEmpty(entry.Guid);
        HasInstance  = hasInstance;
        Level        = hasInstance ? $"+{entry.Level}" : string.Empty;
        SubStatItems = hasInstance
            ? entry.SubStats.Select(s =>
            {
                static string Fmt(double v) =>
                    v == Math.Floor(v) ? ((long)v).ToString() : v.ToString("F1");
                string valueDisplay = s.Rolls.Length > 1
                    ? $"{string.Join(" + ", s.Rolls.Select(Fmt))} = {Fmt(s.Value)}"
                    : Fmt(s.Value);
                return new SubStatItemViewModel(
                    s.Type,
                    valueDisplay,
                    StaticResources.BadgeImage(s.Rolls.Length));
            }).ToList()
            : System.Array.Empty<SubStatItemViewModel>();
        HasInstanceVisibility = hasInstance.ToVisibility();

        if (hasInstance && !string.IsNullOrEmpty(entry.MainStat.TypeRaw))
        {
            var v = meta.GetMainPropValue(entry.Rank, entry.Level, entry.MainStat.TypeRaw);
            MainStatValueDisplay = IsMainPropPercent(entry.MainStat.TypeRaw)
                ? $"{v * 100f:F1}%"
                : ((int)Math.Round(v)).ToString();
        }
        else
        {
            MainStatValueDisplay = string.Empty;
        }

        var slotParts = new System.Collections.Generic.List<string> { entry.Slot, RankDisplay };
        if (hasInstance && entry.Locked) slotParts.Add(BackpackLocalization.Get("Locked"));
        SlotRankEquipped = string.Join("  ", slotParts);

        IconUri = meta.GetIcon(entry.SetName, entry.Slot);

        var allBonuses = meta.GetAllSetBonuses(entry.SetName);
        BonusText             = string.Join("\n", allBonuses.Select(b => $"{b.Count}件套：{b.Desc}"));
        HasAnyBonusVisibility = (allBonuses.Count > 0).ToVisibility();

        QualitySource = StaticResources.QualityImage(entry.Rank);
    }

    private static bool IsMainPropPercent(string propTypeRaw) => propTypeRaw is not
        ("FIGHT_PROP_HP" or "FIGHT_PROP_ATTACK" or "FIGHT_PROP_DEFENSE" or "FIGHT_PROP_ELEMENT_MASTERY");
}
