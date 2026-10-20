/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Helpers;
using FufuLauncher.Models.CycleChallenge;
using FufuLauncher.Models.CycleChallenge.Dto;
using FufuLauncher.Models.CycleChallenge.Mapping;
using FufuLauncher.Services.CycleChallenge;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views.Controls.CycleChallenge;

/// <summary>KPI 卡片展示项（标题已本地化，避免在 XAML 里按 key 查表）。</summary>
/// <param name="Title">卡片标题（已本地化）。</param>
/// <param name="ValueText">主数值文本。</param>
public sealed record SpiralAbyssKpiCard(string Title, string ValueText);

/// <summary>一组榜单的展示项（标题已本地化）。</summary>
/// <param name="Title">榜单标题（已本地化）。</param>
/// <param name="Rows">榜单行。</param>
public sealed record SpiralAbyssRankSectionView(string Title, IReadOnlyList<SpiralAbyssRankRow> Rows)
{
    /// <summary>是否有榜单数据。</summary>
    public bool HasRows => Rows.Count > 0;
}

/// <summary>
/// 深境螺旋展示控件。
/// <para>
/// 对外只暴露 <see cref="Show(string)"/>：传入该期深渊 payload JSON（完整响应或仅 data 节点均可），
/// 内部完成「反序列化 → 纯函数映射 → 绑定刷新」。解析失败只显示错误占位，绝不抛出异常。
/// </para>
/// <para>
/// 绑定策略：XAML 侧统一 OneTime 绑定 + <c>Bindings.Update()</c>，因此本类不需要逐字段实现
/// <c>INotifyPropertyChanged</c>；状态整体替换时一次性刷新，语义更可控。
/// </para>
/// </summary>
public sealed partial class SpiralAbyssPanel : UserControl
{
    /// <summary>构造控件。</summary>
    public SpiralAbyssPanel()
    {
        InitializeComponent();
        Loaded += OnPanelLoaded;
    }

    #region x:Bind 绑定面

    /// <summary>当前展示模型（调试/联调可见，XAML 不直接绑定）。</summary>
    public SpiralAbyssDisplay? Display { get; private set; }

    /// <summary>KPI 卡片：总星 / 最深 / 战斗次数 / 胜次 / 最强一击。</summary>
    public IReadOnlyList<SpiralAbyssKpiCard> KpiCards { get; private set; } = [];

    /// <summary>逐层数据（层数按响应原样，不写死 12 层）。</summary>
    public IReadOnlyList<SpiralAbyssFloor> Floors { get; private set; } = [];

    /// <summary>6 组角色榜单。</summary>
    public IReadOnlyList<SpiralAbyssRankSectionView> RankSections { get; private set; } = [];

    /// <summary>是否显示正常内容区。</summary>
    public bool ShowContent { get; private set; }

    /// <summary>是否显示「本期未参与」空态。</summary>
    public bool ShowEmpty { get; private set; }

    /// <summary>是否显示「未解锁」状态。</summary>
    public bool ShowLocked { get; private set; }

    /// <summary>是否显示解析失败占位。</summary>
    public bool ShowError { get; private set; }

    /// <summary>是否显示角色榜单区。</summary>
    public bool ShowRanks { get; private set; }

    /// <summary>是否显示跳层提示。</summary>
    public bool ShowSkippedFloor { get; private set; }

    /// <summary>被跳过的层/间文本，如 "10-3"。</summary>
    public string SkippedFloorText { get; private set; } = string.Empty;

    /// <summary>空态文案。</summary>
    public string EmptyText { get; private set; } = "CycleChallenge_NotParticipated".GetLocalized();

    /// <summary>未解锁文案。</summary>
    public string LockedText { get; private set; } = "CycleChallenge_NotUnlocked".GetLocalized();

    /// <summary>解析失败文案。</summary>
    public string ErrorText { get; private set; } = "CycleChallenge_LoadFailed".GetLocalized();
    #endregion

    /// <summary>
    /// 展示一期深境螺旋数据。
    /// </summary>
    /// <param name="payloadJson">
    /// 该期 payload JSON：可以是完整米游社响应（<c>{ retcode, message, data }</c>），也可以只是 data 节点。
    /// 空串 / data 为 null 时显示空态；JSON 非法时显示错误占位（不抛异常）。
    /// </param>
    public void Show(string payloadJson)
    {
        try
        {
            var dto = SpiralAbyssPayloadParser.Parse(payloadJson);
            Apply(dto);
        }
        catch (Exception ex)
        {
            // 解析失败不得让页面崩溃：记录调试日志 + 错误占位
            Debug.WriteLine($"[SpiralAbyssPanel] payload 解析失败: {ex}");
            ApplyError();
        }
    }

    /// <summary>
    /// 当前角色的区服（如 cn_gf01 / os_usa），用于时间显示时区：
    /// cn_* 展示 UTC+8，其它展示 UTC。
    /// </summary>
    public string? Region { get; set; }

    /// <summary>把 DTO 映射成展示模型并刷新绑定。</summary>
    private void Apply(SpiralAbyssDto? dto)
    {
        if (dto is null)
        {
            ApplyEmpty();
            return;
        }

        var display = SpiralAbyssMapper.Map(dto, CycleChallengeTime.CreateContext(Region));
        Display = display;

        KpiCards = BuildKpiCards(display);
        Floors = display.Floors;
        RankSections = BuildRankSections(display);
        ShowRanks = display.HasAnyRank;
        ShowSkippedFloor = display.ShowSkippedFloorHint;
        SkippedFloorText = display.SkippedFloorText;

        if (!display.IsUnlocked)
        {
            // 未解锁优先级最高：即使 floors 有内容也不展示（接口在未解锁时可能返回空层）
            SetState(content: false, empty: false, locked: true, error: false);
        }
        else if (!display.HasFloors)
        {
            SetState(content: false, empty: true, locked: false, error: false);
        }
        else
        {
            SetState(content: true, empty: false, locked: false, error: false);
        }

        RefreshBindings();
    }

    private void ApplyEmpty()
    {
        Display = SpiralAbyssDisplay.Empty(timeContext: CycleChallengeTime.CreateContext(Region));
        KpiCards = [];
        Floors = [];
        RankSections = [];
        ShowRanks = false;
        ShowSkippedFloor = false;
        SkippedFloorText = string.Empty;
        SetState(content: false, empty: true, locked: false, error: false);
        RefreshBindings();
    }

    private void ApplyError()
    {
        Display = null;
        KpiCards = [];
        Floors = [];
        RankSections = [];
        ShowRanks = false;
        ShowSkippedFloor = false;
        SkippedFloorText = string.Empty;
        SetState(content: false, empty: false, locked: false, error: true);
        RefreshBindings();
    }

    private void SetState(bool content, bool empty, bool locked, bool error)
    {
        ShowContent = content;
        ShowEmpty = empty;
        ShowLocked = locked;
        ShowError = error;
    }

    private void RefreshBindings()
    {
        // Show() 可能在控件 Loaded 之前调用：此时 x:Bind OneTime 尚未初始化，
        // 不调用 Update()，等 Loaded 时再统一取最新属性值即可。
        if (IsLoaded)
        {
            Bindings.Update();
        }
    }

    private void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
        // 覆盖「先 Show 后加载」的场景：Loaded 时强制按当前属性值重算一次绑定。
        Bindings.Update();
    }

    private static IReadOnlyList<SpiralAbyssKpiCard> BuildKpiCards(SpiralAbyssDisplay display) =>
    [
        new SpiralAbyssKpiCard("Abyss_TotalStar".GetLocalized(), display.StarText),
        new SpiralAbyssKpiCard("Abyss_MaxFloor".GetLocalized(), display.MaxFloorDisplay),
        new SpiralAbyssKpiCard("Abyss_BattleTimes".GetLocalized(), display.BattleTimesText),
        new SpiralAbyssKpiCard("Abyss_WinTimes".GetLocalized(), display.WinTimesText),
        new SpiralAbyssKpiCard("Abyss_StrongestHit".GetLocalized(), display.StrongestHitText),
    ];

    private static IReadOnlyList<SpiralAbyssRankSectionView> BuildRankSections(SpiralAbyssDisplay display) =>
    [
        new SpiralAbyssRankSectionView("Abyss_RevealRank".GetLocalized(), display.RevealRank.Rows),
        new SpiralAbyssRankSectionView("Abyss_DefeatRank".GetLocalized(), display.DefeatRank.Rows),
        new SpiralAbyssRankSectionView("Abyss_DamageRank".GetLocalized(), display.DamageRank.Rows),
        new SpiralAbyssRankSectionView("Abyss_TakeDamageRank".GetLocalized(), display.TakeDamageRank.Rows),
        new SpiralAbyssRankSectionView("Abyss_NormalSkillRank".GetLocalized(), display.NormalSkillRank.Rows),
        new SpiralAbyssRankSectionView("Abyss_EnergySkillRank".GetLocalized(), display.EnergySkillRank.Rows),
    ];
}
