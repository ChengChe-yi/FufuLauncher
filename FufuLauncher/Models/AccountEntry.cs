/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Models;

public class AccountEntry
{
    public string Id
    {
        get; set;
    }           
    public string Stuid
    {
        get; set;
    }
    public string ServerType
    {
        get; set;
    }   
    public string CookieFilePath
    {
        get; set;
    }
    public string Nickname
    {
        get; set;
    }    
    public string AvatarUrl
    {
        get; set;
    }
    public string GameUid
    {
        get; set;
    }
    public DateTime LastLoginTime
    {
        get; set;
    }

    /// <summary>
    /// Cookie 文件格式版本（对应 <see cref="Services.AccountManager.CookieFileVersion"/>）。
    /// 默认 0 表示未知/遗留（旧版 accounts.json 未序列化该字段时反序列化得到 0，
    /// 加载时由 AccountManager 检测并迁移 cookie 文件后归一化为当前版本）。
    /// </summary>
    public int CookieVersion
    {
        get; set;
    } = 0;

    /// <summary>
    /// Cookie 数据最后更新时间。
    /// </summary>
    public DateTime UpdatedAt
    {
        get; set;
    }
}

