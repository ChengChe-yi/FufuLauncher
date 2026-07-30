/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Models.Backpack;
using FufuLauncher.Services.Backpack;

namespace FufuLauncher.ViewModels;

public sealed partial class MaterialViewModel : SimpleItemViewModel
{
    public MaterialViewModel(MaterialEntry entry, MaterialMetaService meta)
        : base(entry.Name, entry.Count, meta.GetMeta(entry.Id)) { }
}
