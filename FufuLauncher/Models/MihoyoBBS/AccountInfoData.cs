/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace MihoyoBBS;

public class AccountInfoData
{
    [JsonPropertyName("list")]
    public List<AccountItem> List
    {
        get;
        set;
    }
}
