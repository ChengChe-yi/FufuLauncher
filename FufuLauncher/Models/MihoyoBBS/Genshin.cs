/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace MihoyoBBS;

public class Genshin : GameCheckin
{
    public Genshin() : base("hk4e_cn", "原神", "e202311201442471", "旅行者") {}
    public override async Task InitializeAsync(Config config)
    {
        SetHeaders(config);
        Headers["Origin"] = "https://act.mihoyo.com";
        Headers["x-rpc-signgame"] = "hk4e";
        Headers["Referer"] = "https://act.mihoyo.com/";

        AccountList = await GetAccountListAsync(config).ConfigureAwait(false);
        if (AccountList?.Count > 0)
        {
            CheckinRewards = await GetCheckinRewardsAsync().ConfigureAwait(false);
        }
    }

}
