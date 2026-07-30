/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.Backpack;

public sealed class MaterialMetaService : TabMetaService
{
    private static readonly (string File, string Key)[] _tabDefs =
    [
        ("materials_char_ascension.json",   "MatTabCharAscension"),
        ("materials_weapon_ascension.json", "MatTabWeaponAscension"),
        ("materials_talent.json",           "MatTabTalent"),
        ("materials_char_exp.json",         "MatTabCharExp"),
        ("materials_weapon_enhance.json",   "MatTabWeaponEnhance"),
        ("materials_refine.json",           "MatTabRefine"),
        ("materials_local_specialty.json",  "MatTabLocalSpecialty"),
        ("materials_ingredient.json",       "MatTabIngredient"),
        ("materials_common.json",           "MatTabCommon"),
        ("materials_ore.json",              "MatTabOre"),
        ("materials_fish.json",             "MatTabFish"),
        ("materials_bait.json",             "MatTabBait"),
    ];

    public MaterialMetaService() : base("Material", _tabDefs) { }
}
