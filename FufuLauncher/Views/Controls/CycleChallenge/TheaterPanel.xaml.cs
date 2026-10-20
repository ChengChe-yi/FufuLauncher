/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Models.CycleChallenge;
using FufuLauncher.Models.CycleChallenge.Mapping;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views.Controls.CycleChallenge;

/// <summary>
/// 幻想真境剧诗展示控件。
/// <para>
/// 使用方式：<see cref="Show"/> 接收服务层给出的单期 payload（JSON 字符串），
/// 内部由 <see cref="RoleCombatMapper"/> 解析为 <see cref="RoleCombatDisplay"/> 后通过 x:Bind 渲染。
/// payload 为空 / 语法错误 / 结构缺字段都不会抛异常到调用方，控件会展示对应的空态或错误占位。
/// </para>
/// </summary>
public sealed partial class TheaterPanel : UserControl
{
    private RoleCombatDisplay _display = RoleCombatDisplay.CreateState(TheaterDisplayState.NotParticipated);

    public TheaterPanel()
    {
        InitializeComponent();
    }

    /// <summary>当前展示模型（x:Bind 的数据源）。</summary>
    public RoleCombatDisplay Display => _display;

    /// <summary>
    /// 当前角色的区服（如 cn_gf01 / os_usa），用于时间显示时区：
    /// cn_* 展示 UTC+8，其它展示 UTC。
    /// </summary>
    public string? Region { get; set; }

    /// <summary>展示一期的剧诗数据。</summary>
    /// <param name="payloadJson">单期 payload JSON（完整响应 / data 节点 / 单期对象均可）。</param>
    public void Show(string payloadJson)
    {
        RoleCombatDisplay display;
        try
        {
            display = RoleCombatMapper.Parse(payloadJson, Region);
        }
        catch (Exception ex)
        {
            // 解析失败只降级为错误占位，不允许把异常抛到页面导航流程
            Debug.WriteLine($"[TheaterPanel] 解析 payload 失败: {ex}");
            display = RoleCombatDisplay.CreateError(ex.Message);
        }

        SetDisplay(display);
    }

    /// <summary>替换展示模型并刷新一次性编译绑定。</summary>
    private void SetDisplay(RoleCombatDisplay display)
    {
        _display = display;

        try
        {
            Bindings.Update();
        }
        catch (Exception ex)
        {
            // 绑定刷新失败同样不能影响页面（极少数情况下控件尚未进入可视树）
            Debug.WriteLine($"[TheaterPanel] 刷新绑定失败: {ex.Message}");
        }
    }

    /// <summary>辉彩祝福汇总描述（含富文本颜色标记）加载时解析。</summary>
    private void OnSplendourSummaryLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBlock block && block.Tag is string text && text.Length > 0)
        {
            CycleChallengeRichTextParser.Apply(block, [text]);
        }
    }

    /// <summary>辉彩祝福悬浮提示：展示各级效果文本。</summary>
    private void OnSplendourTooltipLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBlock block && block.Tag is TheaterSplendourBuff buff)
        {
            CycleChallengeRichTextParser.Apply(block, buff.Effects);
        }
    }

    /// <summary>神秘收获悬浮提示：展示事件效果文本。</summary>
    private void OnBuffTooltipLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is RichTextBlock block && block.Tag is TheaterBuff buff && buff.HasDescription)
        {
            CycleChallengeRichTextParser.Apply(block, [buff.Description]);
        }
    }
}
