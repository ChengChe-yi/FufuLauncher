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
