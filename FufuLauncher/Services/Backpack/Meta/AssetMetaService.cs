/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.Backpack;

public sealed class AssetMetaService : TabMetaService
{
    private static readonly (string File, string Key)[] _tabDefs =
    [
        ("currency.json", "AssetTabCurrency"),
        ("qiyu.json",     "AssetTabQiyu"),
    ];

    public AssetMetaService() : base("Asset", _tabDefs, sortByRank: true) { }
}
