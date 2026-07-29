using FufuLauncher.Models.Backpack;
using FufuLauncher.Services.Backpack;

namespace FufuLauncher.ViewModels;

public sealed partial class AssetViewModel : SimpleItemViewModel
{
    public AssetViewModel(MaterialEntry entry, AssetMetaService meta)
        : base(entry.Name, entry.Count, meta.GetMeta(entry.Id)) { }
}
