/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.Backpack;

public sealed class GadgetMetaService : TabMetaService
{
    private static readonly (string File, string Key)[] _tabDefs =
    [
        ("gadgets_precious.json",    "GadgetTabPrecious"),
        ("gadgets_adventure.json",   "GadgetTabAdventure"),
        ("gadgets_emblem.json",      "GadgetTabEmblem"),
        ("gadgets_wish.json",        "GadgetTabWish"),
        ("gadgets_voucher_hi.json",  "GadgetTabVoucherHi"),
        ("gadgets_voucher_lo.json",  "GadgetTabVoucherLo"),
        ("gadgets_misc.json",        "GadgetTabMisc"),
        ("gadgets_consumable.json",  "GadgetTabConsumable"),
        ("gadgets_quest.json",       "GadgetTabQuest"),
    ];

    public GadgetMetaService() : base("Gadget", _tabDefs, sortByRank: true) { }
}
