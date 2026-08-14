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
        [JsonPropertyName("icon")] public string Icon { get; set; }
        [JsonPropertyName("name")] public string Name { get; set; }
        [JsonPropertyName("cnt")] public int Count { get; set; }
    }

    public class OsCheckinRewardsData
    {
        [JsonPropertyName("awards")] public List<OsRewardItem> Awards { get; set; }
    }
    
    public class OsCheckinCalendarData
    {
        [JsonPropertyName("month")] public int Month { get; set; }

        [JsonPropertyName("awards")] public List<OsRewardItem> Awards { get; set; }
    }
    
    public class CheckinResignInfo
    {
        [JsonPropertyName("resign_cnt_daily")] public int ResignCountDaily { get; set; }
        [JsonPropertyName("resign_cnt_monthly")] public int ResignCountMonthly { get; set; }
        [JsonPropertyName("resign_limit_daily")] public int ResignLimitDaily { get; set; }
        [JsonPropertyName("resign_limit_monthly")] public int ResignLimitMonthly { get; set; }
        [JsonPropertyName("sign_cnt_missed")] public int SignCountMissed { get; set; }
        [JsonPropertyName("coin_cnt")] public int CoinCount { get; set; }
        [JsonPropertyName("coin_cost")] public int CoinCost { get; set; }
        [JsonPropertyName("rule")] public string Rule { get; set; } = "";
        [JsonPropertyName("signed")] public bool Signed { get; set; }
        [JsonPropertyName("sign_days")] public int SignDays { get; set; }
        [JsonPropertyName("cost")] public int Cost { get; set; }
        [JsonPropertyName("month_quality_cnt")] public int MonthQualityCount { get; set; }
        [JsonPropertyName("quality_cnt")] public int QualityCount { get; set; }
        
        public int RemainingMonthly => Math.Max(0, ResignLimitMonthly - ResignCountMonthly);
    }

    public class OsAccountItem
    {
        [JsonPropertyName("nickname")] public string Nickname { get; set; }
        [JsonPropertyName("game_uid")] public string GameUid { get; set; }
        [JsonPropertyName("region")] public string Region { get; set; }
    }

    public class OsAccountInfoData
    {
        [JsonPropertyName("list")] public List<OsAccountItem> List { get; set; }
    }

   
    public class OsIsSignData
    {
        [JsonPropertyName("total_sign_day")] public int TotalSignDay { get; set; }
        [JsonPropertyName("today")] public string Today { get; set; }
        [JsonPropertyName("is_sign")] public bool IsSign { get; set; }
        [JsonPropertyName("first_bind")] public bool FirstBind { get; set; }
    }


    public class OsApiResponse<T>
    {
        [JsonPropertyName("retcode")] public int RetCode { get; set; }
        [JsonPropertyName("message")] public string Message { get; set; }
        [JsonPropertyName("data")] public T Data { get; set; }
    }

    
    public class OsSignResponseData
    {
        [JsonPropertyName("code")] public string Code { get; set; }
        [JsonPropertyName("first_bind")] public bool FirstBind { get; set; }
    }

    public class HoyolabSignResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SuccessCount { get; set; }
        public int FailCount { get; set; }
        public int SkippedCount { get; set; }
    }
    
    public class HoyolabCheckinService
    {
        public string BaseApi { get; set; } = ApiEndpoints.OverseaSignBaseApi;
        public string ActId { get; set; } = ApiEndpoints.OverseaSignActId;
        public string GameBiz { get; set; } = "hk4e_global";

        private HttpClient _httpClient;
        private Dictionary<string, string> _headers;

        public static string LastApiError { get; set; } = string.Empty;
        public static int LastSignDays { get; set; } = 0;
        public static string LastRewardItem { get; set; } = "Status_None".GetLocalized();

        public List<OsAccountItem> AccountList { get; private set; } = new();
        private List<OsRewardItem> _checkinRewards;

        public HoyolabCheckinService()
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            });
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

       
        private void SetHeaders(string cookie)
        {
            var deviceId = Guid.NewGuid().ToString("N");

            _headers = new Dictionary<string, string>
            {
                ["Accept"] = "application/json, text/plain, */*",
                ["Origin"] = "https://act.hoyolab.com",
                ["x-rpc-app_version"] = "2.54.0",
                ["x-rpc-client_type"] = "5",
                ["x-rpc-language"] = "zh-cn",
                ["Referer"] = "https://act.hoyolab.com/",
                ["Accept-Encoding"] = "gzip, deflate",
                ["Accept-Language"] = "zh-CN,en-US;q=0.8",
                ["Cookie"] = cookie,
                ["x-rpc-device_id"] = deviceId,
                ["x-rpc-game_biz"] = GameBiz,
                ["X-Requested-With"] = "com.mihoyo.hoyolab",
                ["User-Agent"] = "Mozilla/5.0 (Linux; Android 13; Pixel 5) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/118.0.0.0 Mobile Safari/537.36 miHoYoBBSOversea/2.54.0"
            };
        }

        private void AddHeaders(HttpRequestMessage request)
        {
            foreach (var h in _headers)
                AddHeader(request, h.Key, h.Value);
        }

        private void AddHeader(HttpRequestMessage request, string key, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            switch (key.ToLower())
            {
                case "cookie":
                    request.Headers.Add("Cookie", value);
                    break;
                case "user-agent":
                    request.Headers.UserAgent.ParseAdd(value);
                    break;
                case "referer":
                    request.Headers.Referrer = new Uri(value);
                    break;
                case "accept-encoding":
                case "accept-language":
                    request.Headers.TryAddWithoutValidation(key, value);
                    break;
                default:
                    request.Headers.Add(key, value);
                    break;
            }
        }

   
        public async Task InitializeAsync(string cookie, List<OsAccountItem>? fallbackAccounts = null)
        {
            LastApiError = string.Empty;
            SetHeaders(cookie);
            AccountList = await GetAccountListAsync();
            if (AccountList.Count == 0 && fallbackAccounts != null && fallbackAccounts.Count > 0)
                AccountList = fallbackAccounts;
            if (AccountList.Count > 0)
                _checkinRewards = await GetCheckinRewardsAsync();
        }

        private async Task<List<OsAccountItem>> GetAccountListAsync()
        {
            try
            {
                var query = $"game_biz={GameBiz}";
                var url = $"https://api-account-os.hoyolab.com/binding/api/getUserGameRolesByCookieToken?{query}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(req);
                var resp = await _httpClient.SendAsync(req);
                var text = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OsApiResponse<OsAccountInfoData>>(text);
                if (result != null && result.RetCode == 0 && result.Data?.List != null)
                    return result.Data.List;
                LastApiError = result?.Message ?? "Checkin_ParseAccountListFailed".GetLocalized();
            }
            catch (Exception ex)
            {
                LastApiError = string.Format("Checkin_GetAccountListException".GetLocalized(), ex.Message);
            }
            return new List<OsAccountItem>();
        }

        private async Task<List<OsRewardItem>> GetCheckinRewardsAsync()
        {
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    var query = $"act_id={ActId}&lang=zh-cn";
                    var url = $"{BaseApi}/event/sol/home?{query}";
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    AddHeaders(req);
                    var resp = await _httpClient.SendAsync(req);
                    var text = await resp.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<OsApiResponse<OsCheckinRewardsData>>(text);
                    if (result != null && result.RetCode == 0 && result.Data?.Awards != null)
                        return result.Data.Awards;
                }
                catch { }
                await Task.Delay(5000);
            }
            return new List<OsRewardItem>();
        }
        
        public async Task<CheckinCalendarData?> GetCheckinCalendarAsync()
        {
            try
            {
                var query = $"act_id={ActId}&lang=zh-cn";
                var url = $"{BaseApi}/event/sol/home?{query}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(req);
                var resp = await _httpClient.SendAsync(req);
                var text = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OsApiResponse<OsCheckinCalendarData>>(text);
                if (result != null && result.RetCode == 0 && result.Data != null)
                {
                    return new CheckinCalendarData
                    {
                        Month = result.Data.Month,
                        Awards = result.Data.Awards?
                            .Select(a => new CalendarRewardItem { Icon = a.Icon, Name = a.Name, Count = a.Count })
                            .ToList() ?? new List<CalendarRewardItem>()
                    };
                }
                LastApiError = result?.Message ?? "Checkin_GetCalendarException".GetLocalized();
            }
            catch (Exception ex)
            {
                LastApiError = string.Format("Checkin_GetCalendarException".GetLocalized(), ex.Message);
            }
            return null;
        }
        
        public async Task<CheckinResignInfo?> GetResignInfoAsync(string region, string uid)
        {
            try
            {
                var query = $"act_id={ActId}&lang=zh-cn&region={region}&uid={uid}";
                var url = $"{BaseApi}/event/sol/resign_info?{query}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(req);
                var resp = await _httpClient.SendAsync(req);
                var text = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OsApiResponse<CheckinResignInfo>>(text);
                if (result != null && result.RetCode == 0 && result.Data != null)
                    return result.Data;
                LastApiError = result?.Message ?? "Checkin_ResignQueryFailed".GetLocalized();
            }
            catch (Exception ex)
            {
                LastApiError = string.Format("Checkin_ResignQueryFailed".GetLocalized(), ex.Message);
            }
            return null;
        }
        
        public async Task<(bool success, string message)> ResignAsync(string region, string uid)
        {
            try
            {
                var body = new { act_id = ActId, region, uid };
                var jsonBody = JsonSerializer.Serialize(body);

                using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseApi}/event/sol/resign?lang=zh-cn");
                foreach (var h in _headers)
                    AddHeader(req, h.Key, h.Value);
                req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var resp = await _httpClient.SendAsync(req);
                var text = await resp.Content.ReadAsStringAsync();
                var data = JsonSerializer.Deserialize<OsApiResponse<OsSignResponseData>>(text);

                if (data == null)
                    return (false, "Checkin_ParseResultFailed".GetLocalized());
                if (data.RetCode == 0 && data.Data?.Code == "ok")
                    return (true, "Checkin_ResignSuccess".GetLocalized());

                string message = data.RetCode switch
                {
                    -5003 => "Checkin_AlreadySignedToday".GetLocalized(),
                    -5005 => "Checkin_ResignLimitExceeded".GetLocalized(),
                    -5007 => "Checkin_ResignNotSigned".GetLocalized(),
                    -5008 => "Checkin_ResignNoDate".GetLocalized(),
                    -5014 => "Checkin_ResignInsufficientCoin".GetLocalized(),
                    _ => string.Format("Checkin_ResignFailed".GetLocalized(), data.RetCode, data.Message)
                };
                return (false, message);
            }
            catch (Exception ex)
            {
                return (false, string.Format("Checkin_ResignFailed".GetLocalized(), "异常", ex.Message));
            }
        }

        public async Task<OsIsSignData> IsSignAsync(string region, string uid)
        {
            try
            {
                var query = $"act_id={ActId}&lang=zh-cn&region={region}&uid={uid}";
                var url = $"{BaseApi}/event/sol/info?{query}";
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                AddHeaders(req);
                var resp = await _httpClient.SendAsync(req);
                var text = await resp.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OsApiResponse<OsIsSignData>>(text);
                if (result != null && result.RetCode == 0 && result.Data != null)
                    return result.Data;
                LastApiError = result?.Message ?? "Checkin_ParseSignStatusFailed".GetLocalized();
            }
            catch (Exception ex)
            {
                LastApiError = string.Format("Checkin_RequestSignStatusException".GetLocalized(), ex.Message);
            }
            return null;
        }

      
        private async Task<HttpResponseMessage> DoSignAsync(OsAccountItem account)
        {
            var headers = new Dictionary<string, string>(_headers);

            for (int i = 1; i <= 4; i++)
            {
                try
                {
                    var body = new { act_id = ActId, region = account.Region, uid = account.GameUid };
                    var jsonBody = JsonSerializer.Serialize(body);

                    using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseApi}/event/sol/sign");
                    foreach (var h in headers)
                        AddHeader(req, h.Key, h.Value);

                    req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    var resp = await _httpClient.SendAsync(req);
                    if ((int)resp.StatusCode == 429)
                    {
                        await Task.Delay(10000);
                        continue;
                    }
                    return resp;
                }
                catch { return null; }
            }
            return null;
        }

   
        public async Task<string> SignAccountAsync(string cookie, HashSet<string> disabledUids = null)
        {
            var signResult = await SignAccountWithResultAsync(cookie, disabledUids);
            return signResult.Message;
        }

        public async Task<HoyolabSignResult> SignAccountWithResultAsync(string cookie, HashSet<string> disabledUids = null, string targetUid = null)
        {
            LastApiError = string.Empty;
            var message = "HoYoLAB: ";
            var signResult = new HoyolabSignResult();

            if (AccountList.Count == 0)
            {
                message += "Checkin_NoBoundAccount".GetLocalized();
                if (!string.IsNullOrEmpty(LastApiError))
                    message += string.Format("Checkin_Reason".GetLocalized(), LastApiError);
                signResult.FailCount++;
                signResult.Message = message;
                return signResult;
            }

            foreach (var account in AccountList)
            {
                if (disabledUids != null && disabledUids.Contains(account.GameUid))
                {
                    signResult.SkippedCount++;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(targetUid) && account.GameUid != targetUid)
                {
                    signResult.SkippedCount++;
                    continue;
                }

                await Task.Delay(new Random().Next(2000, 8000));

                var isData = await IsSignAsync(account.Region, account.GameUid);
                if (isData == null)
                {
                    message += "\n" + account.Nickname + "Checkin_GetInfoFailed".GetLocalized();
                    if (!string.IsNullOrEmpty(LastApiError))
                        message += string.Format("Checkin_Detail".GetLocalized(), LastApiError);
                    signResult.FailCount++;
                    continue;
                }

                if (isData.FirstBind)
                {
                    message += "\n" + account.Nickname + "Checkin_FirstBindWarning".GetLocalized();
                    signResult.FailCount++;
                    continue;
                }

                var signDays = isData.TotalSignDay;

                if (isData.IsSign)
                {
                    message += "\n" + account.Nickname + "Checkin_AlreadySignedToday".GetLocalized();
                    var idx = signDays - 1;
                    if (_checkinRewards != null && idx >= 0 && idx < _checkinRewards.Count)
                        message += "\n" + string.Format("Checkin_TodayReward".GetLocalized(), FormatItem(_checkinRewards[idx]));
                }
                else
                {
                    await Task.Delay(new Random().Next(2000, 8000));

                    var req = await DoSignAsync(account);
                    if (req == null)
                    {
                        message += "\n" + account.Nickname + "Checkin_SignRequestFailed".GetLocalized();
                        if (!string.IsNullOrEmpty(LastApiError))
                            message += string.Format("Checkin_Detail".GetLocalized(), LastApiError);
                        signResult.FailCount++;
                        continue;
                    }

                    if ((int)req.StatusCode == 429)
                    {
                        message += "\n" + account.Nickname + "Checkin_SignRateLimited".GetLocalized();
                        signResult.FailCount++;
                        continue;
                    }

                    var text = await req.Content.ReadAsStringAsync();
                    var data = JsonSerializer.Deserialize<OsApiResponse<OsSignResponseData>>(text);

                    if (data == null)
                    {
                        message += "\n" + account.Nickname + "Checkin_ParseResultFailed".GetLocalized();
                        signResult.FailCount++;
                        continue;
                    }

                    if (data.RetCode == 0 && data.Data?.Code == "ok")
                    {
                        signDays++;
                        message += "\n" + account.Nickname + "Checkin_SignSuccess".GetLocalized();
                    }
                    else if (data.RetCode == -5003)
                    {
                        message += "\n" + account.Nickname + "Checkin_AlreadySignedToday".GetLocalized();
                    }
                    else
                    {
                        message += "\n" + account.Nickname + string.Format("Checkin_SignFailedApi".GetLocalized(), data.Message);
                        signResult.FailCount++;
                        continue;
                    }
                }

                signResult.SuccessCount++;
                message += "\n" + account.Nickname + string.Format("Checkin_SignedDays".GetLocalized(), signDays);
                LastSignDays = signDays;

                var rewardIdx = signDays - 1;
                if (_checkinRewards != null && rewardIdx >= 0 && rewardIdx < _checkinRewards.Count)
                {
                    LastRewardItem = FormatItem(_checkinRewards[rewardIdx]);
                    message += "\n" + string.Format("Checkin_RewardIs".GetLocalized(), LastRewardItem);
                }
            }

            if (signResult.SuccessCount == 0 && signResult.FailCount == 0)
                message += "\n" + "Checkin_NoAccountToSign".GetLocalized();

            signResult.Success = signResult.SuccessCount > 0 && signResult.FailCount == 0;
            signResult.Message = message;
            return signResult;
        }

        private string FormatItem(OsRewardItem item)
        {
            return $"「{item.Name}」x{item.Count}";
        }
    }
}
