using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.Services.Backpack;

internal static class StaticResources
{
    private const string Base = "http://8.134.75.17/static/raw/";
    private static readonly Dictionary<int, BitmapImage> _qualityImages = [];
    private static readonly Dictionary<int, BitmapImage> _badgeImages = [];

    public static Uri WeaponIcon(string icon)   => new($"{Base}EquipIcon/{icon}.png");
    public static Uri ArtifactIcon(string icon) => new($"{Base}RelicIcon/{icon}.png");
    public static Uri MaterialIcon(string icon) => new($"{Base}ItemIcon/{icon}.png");

    public static Uri QualityIcon(int rank)
    {
        var name = rank switch
        {
            5 => "UI_QUALITY_ORANGE",
            4 => "UI_QUALITY_PURPLE",
            3 => "UI_QUALITY_BLUE",
            2 => "UI_QUALITY_GREEN",
            _ => "UI_QUALITY_WHITE",
        };
        return new($"ms-appx:///Assets/Backpack/Quality/{name}.png");
    }

    public static BitmapImage QualityImage(int rank)
    {
        rank = Math.Clamp(rank, 1, 5);
        if (_qualityImages.TryGetValue(rank, out var image)) return image;
        image = new BitmapImage(QualityIcon(rank));
        _qualityImages[rank] = image;
        return image;
    }

    public static BitmapImage BadgeImage(int rolls)
    {
        rolls = Math.Clamp(rolls, 1, 11);
        if (_badgeImages.TryGetValue(rolls, out var image)) return image;
        image = new BitmapImage(new Uri($"ms-appx:///Assets/Backpack/badge/badge-{rolls}.ico"));
        _badgeImages[rolls] = image;
        return image;
    }
}
