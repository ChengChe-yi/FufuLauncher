using FufuLauncher.Services.Backpack;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;

namespace FufuLauncher.ViewModels;

public abstract class SimpleItemViewModel : ObservableObject
{
    public string Name { get; }
    public string Count { get; }
    public ulong CountValue { get; }
    public int Rank { get; }
    public Uri? IconUri { get; }
    public BitmapImage QualitySource { get; }

    protected SimpleItemViewModel(string name, ulong count, (Uri? IconUri, int Rank) meta)
    {
        Name = name;
        Count = count.ToString("N0");
        CountValue = count;
        Rank = meta.Rank;
        IconUri = meta.IconUri;
        QualitySource = StaticResources.QualityImage(meta.Rank);
    }
}
