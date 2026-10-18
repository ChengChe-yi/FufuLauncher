/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FufuLauncher.Constants;
using FufuLauncher.Helpers;
using MihoyoBBS;

namespace FufuLauncher.Models
{
    public class OsRewardItem
    {
        [JsonPropertyName("icon")]
        public string Icon
        {
            get;
            set;
        }

        [JsonPropertyName("name")]
        public string Name
        {
            get;
            set;
        }

        [JsonPropertyName("cnt")]
        public int Count
        {
            get;
            set;
        }
    }

    public class OsCheckinRewardsData
    {
        [JsonPropertyName("awards")]
        public List<OsRewardItem> Awards
        {
            get;
            set;
        }
    }

    public class OsCheckinCalendarData
    {
        [JsonPropertyName("month")]
        public int Month
        {
            get;
            set;
        }

        [JsonPropertyName("awards")]
        public List<OsRewardItem> Awards
        {
            get;
            set;
        }
    }

    public class CheckinResignInfo
    {
        [JsonPropertyName("resign_cnt_daily")]
        public int ResignCountDaily
        {
            get;
            set;
        }

        [JsonPropertyName("resign_cnt_monthly")]
        public int ResignCountMonthly
        {
            get;
            set;
        }

        [JsonPropertyName("resign_limit_daily")]
        public int ResignLimitDaily
        {
            get;
            set;
        }

        [JsonPropertyName("resign_limit_monthly")]
        public int ResignLimitMonthly
        {
            get;
            set;
        }

        [JsonPropertyName("sign_cnt_missed")]
        public int SignCountMissed
        {
            get;
            set;
        }

        [JsonPropertyName("coin_cnt")]
        public int CoinCount
        {
            get;
            set;
        }

        [JsonPropertyName("coin_cost")]
        public int CoinCost
        {
            get;
            set;
        }

        [JsonPropertyName("rule")]
        public string Rule
        {
            get;
            set;
        } = "";

        [JsonPropertyName("signed")]
        public bool Signed
        {
            get;
            set;
        }

        [JsonPropertyName("sign_days")]
        public int SignDays
        {
            get;
            set;
        }

        [JsonPropertyName("cost")]
        public int Cost
        {
            get;
            set;
        }

        [JsonPropertyName("month_quality_cnt")]
        public int MonthQualityCount
        {
            get;
            set;
        }

        [JsonPropertyName("quality_cnt")]
        public int QualityCount
        {
            get;
            set;
        }

        public int RemainingMonthly => Math.Max(0, ResignLimitMonthly - ResignCountMonthly);
    }

    public class OsAccountItem
    {
        [JsonPropertyName("nickname")]
        public string Nickname
        {
            get;
            set;
        }

        [JsonPropertyName("game_uid")]
        public string GameUid
        {
            get;
            set;
        }

        [JsonPropertyName("region")]
        public string Region
        {
            get;
            set;
        }
    }

    public class OsAccountInfoData
    {
        [JsonPropertyName("list")]
        public List<OsAccountItem> List
        {
            get;
            set;
        }
    }


    public class OsIsSignData
    {
        [JsonPropertyName("total_sign_day")]
        public int TotalSignDay
        {
            get;
            set;
        }

        [JsonPropertyName("today")]
        public string Today
        {
            get;
            set;
        }

        [JsonPropertyName("is_sign")]
        public bool IsSign
        {
            get;
            set;
        }

        [JsonPropertyName("first_bind")]
        public bool FirstBind
        {
            get;
            set;
        }
    }


    public class OsApiResponse<T>
    {
        [JsonPropertyName("retcode")]
        public int RetCode
        {
            get;
            set;
        }

        [JsonPropertyName("message")]
        public string Message
        {
            get;
            set;
        }

        [JsonPropertyName("data")]
        public T Data
        {
            get;
            set;
        }
    }


    public class OsSignResponseData
    {
        [JsonPropertyName("code")]
        public string Code
        {
            get;
            set;
        }

        [JsonPropertyName("first_bind")]
        public bool FirstBind
        {
            get;
            set;
        }
    }

    public class HoyolabSignResult
    {
        public bool Success
        {
            get;
            set;
        }

        public string Message
        {
            get;
            set;
        } = string.Empty;

        public int SuccessCount
        {
            get;
            set;
        }

        public int FailCount
        {
            get;
            set;
        }

        public int SkippedCount
        {
            get;
            set;
        }
    }

}