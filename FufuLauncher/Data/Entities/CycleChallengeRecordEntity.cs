/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FufuLauncher.Data.Entities;

[Table("CycleChallengeRecord")]
public class CycleChallengeRecordEntity
{
    [Column("Uid")]
    [Key]
    public string Uid
    {
        get;
        set;
    } = string.Empty;


    [Column("Mode")]
    [Key]
    public int Mode
    {
        get;
        set;
    }


    [Column("ScheduleId")]
    [Key]
    public string ScheduleId
    {
        get;
        set;
    } = string.Empty;


    [Column("AccountId")]
    public string? AccountId
    {
        get;
        set;
    }

    [Column("StartUtc")]
    public string? StartUtc
    {
        get;
        set;
    }

    [Column("EndUtc")]
    public string? EndUtc
    {
        get;
        set;
    }


    [Column("Summary")]
    public string? Summary
    {
        get;
        set;
    }


    [Column("Payload")]
    public string Payload
    {
        get;
        set;
    } = string.Empty;

    [Column("FetchedAt")]
    public string FetchedAt
    {
        get;
        set;
    } = string.Empty;

    [Column("ApiVersion")]
    public string? ApiVersion
    {
        get;
        set;
    }
}