/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FufuLauncher.Data.Entities;

/// <summary>
/// 周期挑战记录：以 (Uid, Mode, ScheduleId) 为唯一键，同期刷新时整行覆盖。
/// Payload 保存该期 API 返回的 JSON 原文，Summary 仅用于列表展示。
/// </summary>
[Table("CycleChallengeRecord")]
public class CycleChallengeRecordEntity
{
    /// <summary>角色 UID（唯一归属，字符串保存以避免前导零/溢出问题）。</summary>
    [Column("Uid")]
    [Key]
    public string Uid { get; set; } = string.Empty;

    /// <summary>挑战模式，取值见 <see cref="FufuLauncher.Models.CycleChallenge.ChallengeMode"/>。</summary>
    [Column("Mode")]
    [Key]
    public int Mode { get; set; }

    /// <summary>官方期次 ID（深境/剧诗为数字串，危战为 5269xxx 串）。</summary>
    [Column("ScheduleId")]
    [Key]
    public string ScheduleId { get; set; } = string.Empty;

    /// <summary>写入该记录时的社区账号 ID，仅用于审计，不参与主键。</summary>
    [Column("AccountId")]
    public string? AccountId { get; set; }

    [Column("StartUtc")]
    public string? StartUtc { get; set; }

    [Column("EndUtc")]
    public string? EndUtc { get; set; }

    /// <summary>列表摘要 JSON（星数/难度/徽记/用时等），避免读取大 Payload。</summary>
    [Column("Summary")]
    public string? Summary { get; set; }

    /// <summary>该期完整响应 JSON 原文。</summary>
    [Column("Payload")]
    public string Payload { get; set; } = string.Empty;

    [Column("FetchedAt")]
    public string FetchedAt { get; set; } = string.Empty;

    [Column("ApiVersion")]
    public string? ApiVersion { get; set; }
}
