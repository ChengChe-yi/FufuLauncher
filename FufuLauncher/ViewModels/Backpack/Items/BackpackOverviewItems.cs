using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.ViewModels;

public sealed record BackpackKpiItem(string Glyph, string Value, string Label, string ColorTag = "accent");
public sealed record BackpackInsightItem(string Glyph, string Title, string Body, string ColorTag = "accent");
public sealed record BackpackPlanItem(string Name, string OwnedText, double Progress, string ColorTag = "accent");
public sealed record BackpackCookingItem(
    string Name,
    string Status,
    string MissingText,
    bool IsCookable,
    string ColorTag,
    Uri? IconUri = null,
    BitmapImage? QualitySource = null);
