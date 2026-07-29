using FufuLauncher.Models.Backpack;
using FufuLauncher.Services.Backpack;

namespace FufuLauncher.ViewModels;

public sealed partial class GadgetViewModel : SimpleItemViewModel
{
    public GadgetViewModel(MaterialEntry entry, GadgetMetaService meta)
        : base(entry.Name, entry.Count, meta.GetMeta(entry.Id)) { }
}
