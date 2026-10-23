/*
Copyright (c) FufuLauncher Dev Team. All Rights Reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using System.Globalization;
using System.Text;
using FufuLauncher.Helpers;
using FufuLauncher.Models.CycleChallenge;
using FufuLauncher.Models.CycleChallenge.Mapping;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FufuLauncher.Views.Controls.CycleChallenge;

/// <summary>
///     幽境危战展示控件。
///     <para>
///         使用方式：<see cref="Show" /> 接收服务层给出的单期 payload，由
///         <see cref="HardChallengeMapper" /> 解析为 <see cref="HardChallengeDisplay" /> 后渲染。
///         顶部两个 ToggleButton（单人 / 联机）切换当前展示的 <see cref="StygianMode" />；
///         <c>mp.has_data=false</c> 时联机按钮被禁用并显示「暂无多人挑战数据」。
///         怪物描述里的 <c>&lt;color=#RRGGBBAA&gt;...&lt;/color&gt;</c> 由
///         <see cref="CycleChallengeRichTextParser" /> 解析为 <see cref="RichTextBlock" /> 的 Paragraph / Run 集合。
///     </para>
/// </summary>
public sealed partial class StygianPanel : UserControl
{
    private HardChallengeDisplay _display = HardChallengeDisplay.CreateState(StygianDisplayState.NotParticipated);
    private bool _showCoop;

    public StygianPanel()
    {
        InitializeComponent();
    }

    /// <summary>当前展示模型（x:Bind 的数据源）。</summary>
    public HardChallengeDisplay Display => _display;

    /// <summary>
    ///     当前角色的区服（如 cn_gf01 / os_usa），用于时间显示时区：
    ///     cn_* 展示 UTC+8，其它展示 UTC。
    /// </summary>
    public string? Region
    {
        get;
        set;
    }

    /// <summary>当前选中的单人 / 联机数据。</summary>
    public StygianMode Mode => _showCoop ? _display.Coop : _display.Solo;

    /// <summary>单人按钮是否选中（切换后通过 Bindings.Update 同步）。</summary>
    public bool IsSoloSelected => _display.ShowContent && !_showCoop;

    /// <summary>联机按钮是否选中。</summary>
    public bool IsCoopSelected => _display.ShowContent && _showCoop;

    /// <summary>是否需要提示「暂无多人挑战数据」。</summary>
    public bool ShowNoCoopHint => _display.ShowContent && !_display.HasCoopData;

    /// <summary>当前模式是否没有数据（显示空态文案）。</summary>
    public bool ShowModeEmpty => _display.ShowContent && !Mode.HasData;

    /// <summary>展示一期的危战数据。</summary>
    /// <param name="payloadJson">单期 payload JSON（完整响应 / data 节点 / 单期对象均可）。</param>
    public void Show(string payloadJson)
    {
        HardChallengeDisplay display;
        try
        {
            display = HardChallengeMapper.Parse(payloadJson, Region);
        }
        catch (Exception ex)
        {
            // 解析失败只降级为错误占位，不允许把异常抛到页面导航流程
            DebugLog.Info("StygianPanel", $"[StygianPanel] Failed to parse payload: {ex}");
            display = HardChallengeDisplay.CreateError(ex.Message);
        }

        SetDisplay(display);
    }

    /// <summary>替换展示模型、重置到默认模式并刷新绑定。</summary>
    private void SetDisplay(HardChallengeDisplay display)
    {
        _display = display;

        // 默认展示单人；单人没有数据而联机有时才默认联机
        _showCoop = !display.Solo.HasData && display.Coop.HasData;

        SoloChip.IsEnabled = display.Solo.HasData;
        CoopChip.IsEnabled = display.Coop.HasData;
        ToolTipService.SetToolTip(
            CoopChip,
            display.HasCoopData ? null : "Stygian_NoCoopData".GetLocalized());

        RefreshBindings();
    }

    /// <summary>刷新一次性编译绑定，并保证两个切换按钮的选中态与当前模式一致。</summary>
    private void RefreshBindings()
    {
        try
        {
            Bindings.Update();
        }
        catch (Exception ex)
        {
            // 绑定刷新失败不能影响页面（极少数情况下控件尚未进入可视树）
            DebugLog.Info("StygianPanel", $"[StygianPanel] Refresh binding failed: {ex.Message}");
        }

        // 程序化设置 IsChecked 不会再次触发 Click，仅用于同步视觉状态
        SoloChip.IsChecked = IsSoloSelected;
        CoopChip.IsChecked = IsCoopSelected;
    }

    private void OnSoloChipClick(object sender, RoutedEventArgs e)
    {
        if (!_display.Solo.HasData)
        {
            RefreshBindings();
            return;
        }

        _showCoop = false;
        RefreshBindings();
    }

    private void OnCoopChipClick(object sender, RoutedEventArgs e)
    {
        if (!_display.HasCoopData)
        {
            // 联机无数据时按钮本身已禁用；这里再兜底一次，避免被键盘 / 自动化触发
            RefreshBindings();
            return;
        }

        _showCoop = true;
        RefreshBindings();
    }

    /// <summary>怪物描述 RichTextBlock 加载完成：把富文本解析成 Paragraph / Run 集合。</summary>
    private void OnMonsterDescriptionLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not RichTextBlock block || block.Tag is not StygianMonster monster)
        {
            return;
        }

        try
        {
            CycleChallengeRichTextParser.Apply(block, monster.Descriptions);
        }
        catch (Exception ex)
        {
            // 富文本解析失败时退回纯文本，确保信息不丢失
            DebugLog.Info("StygianPanel", $"[StygianPanel] Failed to parse monster description rich text: {ex.Message}");
            block.Blocks.Clear();
            var paragraph = new Paragraph();
            paragraph.Inlines.Add(new Run { Text = string.Join(Environment.NewLine, monster.Descriptions) });
            block.Blocks.Add(paragraph);
        }
    }
}

/// <summary>
///     危战怪物描述的小型富文本解析器。
///     <para>
///         支持的标记（均来自米游社真实响应）：
///         <list type="bullet">
///             <item>
///                 <c>&lt;color=#RRGGBBAA&gt;文本&lt;/color&gt;</c> → <see cref="Run" /> 的
///                 <see cref="TextElement.Foreground" />（8 位十六进制按 RRGGBBAA 解析，6 位按 RRGGBB 解析并补全不透明）；
///             </item>
///             <item>换行：真实换行符 <c>\n</c> 与字面量 <c>\\n</c> 都转成 <see cref="LineBreak" />；</item>
///             <item>
///                 未知 / 不完整标签（如 <c>&lt;b&gt;</c>、缺失闭合的 <c>&lt;color=...&gt;</c>）按纯文本原样输出，
///                 不会吞掉文本，也不会抛异常。
///             </item>
///         </list>
///         每个 desc 数组元素生成一个 <see cref="Paragraph" />，空元素直接跳过。
///     </para>
/// </summary>
internal static class CycleChallengeRichTextParser
{
    private const string ColorOpenPrefix = "<color=";
    private const string ColorCloseTag = "</color>";
    private const string LinkOpenPrefix = "{LINK#";
    private const string LinkCloseTag = "{/LINK}";

    /// <summary>链接文本颜色（米游社 LINK 语法统一使用的高亮色）。</summary>
    private static readonly Color LinkForeground = Color.FromArgb(0xFF, 0xF3, 0x90, 0x01);

    /// <summary>把若干条描述解析并写入目标 RichTextBlock（会先清空既有 Blocks）。</summary>
    /// <param name="target">目标 RichTextBlock。</param>
    /// <param name="descriptions">怪物描述原文（保留颜色标记）。</param>
    public static void Apply(RichTextBlock target, IReadOnlyList<string>? descriptions)
    {
        target.Blocks.Clear();
        if (descriptions is null || descriptions.Count == 0)
        {
            return;
        }

        foreach (var description in descriptions)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            var paragraph = BuildParagraph(description);
            if (paragraph.Inlines.Count > 0)
            {
                target.Blocks.Add(paragraph);
            }
        }
    }

    /// <summary>把单条描述解析为一个 <see cref="Paragraph" />（纯函数，便于单独验证）。</summary>
    /// <param name="text">描述原文。</param>
    /// <returns>包含 Run / LineBreak 的段落。</returns>
    internal static Paragraph BuildParagraph(string text)
    {
        var paragraph = new Paragraph();
        Underline? currentLink = null;

        foreach (var segment in Tokenize(text))
        {
            switch (segment.Kind)
            {
                case SegmentKind.LinkOpen:
                    // 嵌套 / 重复的 link 开标签：先把未闭合的上一个链接挂上去
                    if (currentLink is not null)
                    {
                        paragraph.Inlines.Add(currentLink);
                    }

                    // 用 Underline（Span 派生）只做“下划线 + 颜色”的视觉强调；
                    // Link 标签本身没有可导航目标，避免 Hyperlink 的 Tab 焦点/手型光标/读屏链接语义。
                    currentLink = new Underline
                    {
                        Foreground = new SolidColorBrush(LinkForeground),
                    };
                    continue;

                case SegmentKind.LinkClose:
                    if (currentLink is not null)
                    {
                        paragraph.Inlines.Add(currentLink);
                        currentLink = null;
                    }

                    continue;

                case SegmentKind.LineBreak:
                    AddInline(paragraph, currentLink, new LineBreak());
                    continue;
            }

            if (segment.Text.Length == 0)
            {
                continue;
            }

            var run = new Run { Text = segment.Text };
            if (segment.Foreground is { } color)
            {
                run.Foreground = new SolidColorBrush(color);
            }

            AddInline(paragraph, currentLink, run);
        }

        // 未闭合的 {LINK#...}：内容仍然按链接样式展示，避免吞掉文本
        if (currentLink is not null)
        {
            paragraph.Inlines.Add(currentLink);
        }

        return paragraph;
    }

    private static void AddInline(Paragraph paragraph, Span? link, Inline inline)
    {
        if (link is not null)
        {
            link.Inlines.Add(inline);
        }
        else
        {
            paragraph.Inlines.Add(inline);
        }
    }

    /// <summary>解析出的文本片段种类。</summary>
    private enum SegmentKind
    {
        Text,
        LineBreak,
        LinkOpen,
        LinkClose,
    }

    /// <summary>解析出的文本片段；<see cref="Foreground" /> 为 null 表示继承 RichTextBlock 的前景色。</summary>
    /// <param name="Text">文本内容（换行 / 链接标记片段为空串）。</param>
    /// <param name="Foreground">颜色，null 表示继承。</param>
    /// <param name="Kind">片段种类。</param>
    private readonly record struct RichSegment(string Text, Color? Foreground, SegmentKind Kind);

    private static List<RichSegment> Tokenize(string text)
    {
        var segments = new List<RichSegment>();
        TokenizeCore(text, null, segments);
        return segments;
    }

    private static void TokenizeCore(string text, Color? inheritedColor, List<RichSegment> segments)
    {
        var plain = new StringBuilder();

        void FlushPlain()
        {
            if (plain.Length > 0)
            {
                segments.Add(new RichSegment(plain.ToString(), inheritedColor, SegmentKind.Text));
                plain.Clear();
            }
        }

        var index = 0;
        while (index < text.Length)
        {
            var current = text[index];

            // 换行：真实换行符
            if (current == '\n')
            {
                FlushPlain();
                segments.Add(new RichSegment(string.Empty, inheritedColor, SegmentKind.LineBreak));
                index++;
                continue;
            }

            // 换行：字面量 \n（部分接口会返回转义后的两字符序列）
            if (current == '\\' && index + 1 < text.Length && text[index + 1] == 'n')
            {
                FlushPlain();
                segments.Add(new RichSegment(string.Empty, inheritedColor, SegmentKind.LineBreak));
                index += 2;
                continue;
            }

            // 链接标签：{LINK#N211101}...{/LINK}
            if (current == '{' && TryReadLinkTag(text, index, out var linkKind, out var linkEnd))
            {
                FlushPlain();
                segments.Add(new RichSegment(string.Empty, inheritedColor, linkKind));
                index = linkEnd;
                continue;
            }

            // 颜色标签
            if (current == '<' && TryReadColorTag(text, index, out var contentStart, out var color))
            {
                var closeStart = text.IndexOf(ColorCloseTag, contentStart, StringComparison.OrdinalIgnoreCase);
                if (closeStart >= 0)
                {
                    FlushPlain();
                    var inner = text[contentStart..closeStart];
                    // 递归解析内层（支持内层换行 / 未知标签），颜色继承当前标签
                    TokenizeCore(inner, color, segments);
                    index = closeStart + ColorCloseTag.Length;
                    continue;
                }

                // 缺少闭合标签：按纯文本处理（下面逐字符追加）
            }

            // 其余字符（含未知标签）按纯文本处理
            plain.Append(current);
            index++;
        }

        FlushPlain();
    }

    /// <summary>尝试读取 <c>{LINK#...}</c> / <c>{/LINK}</c> 链接标记。</summary>
    /// <param name="text">完整文本。</param>
    /// <param name="startIndex">'{' 所在下标。</param>
    /// <param name="kind">解析出的片段种类（<see cref="SegmentKind.LinkOpen" /> / <see cref="SegmentKind.LinkClose" />）。</param>
    /// <param name="nextIndex">标记之后的第一个字符下标。</param>
    /// <returns>是否为合法的链接标记。</returns>
    private static bool TryReadLinkTag(string text, int startIndex, out SegmentKind kind, out int nextIndex)
    {
        kind = SegmentKind.Text;
        nextIndex = startIndex;

        var span = text.AsSpan(startIndex);
        if (span.StartsWith(LinkCloseTag, StringComparison.OrdinalIgnoreCase))
        {
            kind = SegmentKind.LinkClose;
            nextIndex = startIndex + LinkCloseTag.Length;
            return true;
        }

        if (!span.StartsWith(LinkOpenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var tagEnd = text.IndexOf('}', startIndex);
        if (tagEnd < 0)
        {
            return false;
        }

        kind = SegmentKind.LinkOpen;
        nextIndex = tagEnd + 1;
        return true;
    }

    /// <summary>尝试读取 <c>&lt;color=#RRGGBBAA&gt;</c> 开标签。</summary>
    /// <param name="text">完整文本。</param>
    /// <param name="startIndex">'&lt;' 所在下标。</param>
    /// <param name="contentStart">颜色文本之后（'>' 的下一个字符）的下标。</param>
    /// <param name="color">解析出的颜色。</param>
    /// <returns>是否为合法的颜色开标签。</returns>
    private static bool TryReadColorTag(string text, int startIndex, out int contentStart, out Color color)
    {
        contentStart = 0;
        color = default;

        if (!text.AsSpan(startIndex).StartsWith(ColorOpenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var hexStart = startIndex + ColorOpenPrefix.Length;
        var tagEnd = text.IndexOf('>', hexStart);
        if (tagEnd < 0)
        {
            return false;
        }

        var hex = text[hexStart..tagEnd];
        if (!TryParseColor(hex, out color))
        {
            return false;
        }

        contentStart = tagEnd + 1;
        return true;
    }

    /// <summary>解析 <c>#RRGGBBAA</c> / <c>#RRGGBB</c> / <c>#RGB</c> 颜色文本。</summary>
    /// <param name="raw">颜色文本（可带 #）。</param>
    /// <param name="color">解析结果。</param>
    /// <returns>是否解析成功。</returns>
    private static bool TryParseColor(string? raw, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var hex = raw.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        return hex.Length switch
        {
            // 米游社真实格式：#RRGGBBAA
            8 => TryParseHex(hex, out color, withAlpha: true),
            6 => TryParseHex(hex, out color, withAlpha: false),
            // 少数场景可能是 #RGB / #RGBA，做一次展开兜底
            3 => TryParseShortHex(hex, out color),
            4 => TryParseShortHex(hex, out color),
            _ => false,
        };
    }

    private static bool TryParseHex(string hex, out Color color, bool withAlpha)
    {
        color = default;

        if (!TryParseByte(hex.AsSpan(0, 2), out var r)
            || !TryParseByte(hex.AsSpan(2, 2), out var g)
            || !TryParseByte(hex.AsSpan(4, 2), out var b))
        {
            return false;
        }

        byte a = 0xFF;
        if (withAlpha && !TryParseByte(hex.AsSpan(6, 2), out a))
        {
            return false;
        }

        color = Color.FromArgb(a, r, g, b);
        return true;
    }

    private static bool TryParseShortHex(string hex, out Color color)
    {
        color = default;

        if (!TryParseNibble(hex[0], out var r)
            || !TryParseNibble(hex[1], out var g)
            || !TryParseNibble(hex[2], out var b))
        {
            return false;
        }

        byte a = 0xFF;
        if (hex.Length == 4 && !TryParseNibble(hex[3], out a))
        {
            return false;
        }

        color = Color.FromArgb(a, r, g, b);
        return true;
    }

    private static bool TryParseByte(ReadOnlySpan<char> span, out byte value) =>
        byte.TryParse(span, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

    private static bool TryParseNibble(char c, out byte value)
    {
        value = 0;
        if (!byte.TryParse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var nibble))
        {
            return false;
        }

        value = (byte)((nibble << 4) | nibble);
        return true;
    }
}