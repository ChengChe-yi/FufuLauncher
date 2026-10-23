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

public sealed record SpiralAbyssKpiCard(string Title, string ValueText);

public sealed record SpiralAbyssRankSectionView(string Title, IReadOnlyList<SpiralAbyssRankRow> Rows)
{
    public bool HasRows => Rows.Count > 0;
}

public sealed partial class SpiralAbyssPanel : UserControl
{
    public SpiralAbyssPanel()
    {
        InitializeComponent();
        Loaded += OnPanelLoaded;
    }

    #region x:Bind 绑定面

    public SpiralAbyssDisplay? Display
    {
        get;
        private set;
    }


    public IReadOnlyList<SpiralAbyssKpiCard> KpiCards
    {
        get;
        private set;
    } = [];


    public IReadOnlyList<SpiralAbyssFloor> Floors
    {
        get;
        private set;
    } = [];


    public IReadOnlyList<SpiralAbyssRankSectionView> RankSections
    {
        get;
        private set;
    } = [];


    public bool ShowContent
    {
        get;
        private set;
    }


    public bool ShowEmpty
    {
        get;
        private set;
    }


    public bool ShowLocked
    {
        get;
        private set;
    }


    public bool ShowError
    {
        get;
        private set;
    }


    public bool ShowRanks
    {
        get;
        private set;
    }


    public bool ShowSkippedFloor
    {
        get;
        private set;
    }


    public string SkippedFloorText
    {
        get;
        private set;
    } = string.Empty;


    public string EmptyText
    {
        get;
        private set;
    } = "CycleChallenge_NotParticipated".GetLocalized();


    public string LockedText
    {
        get;
        private set;
    } = "CycleChallenge_NotUnlocked".GetLocalized();


    public string ErrorText
    {
        get;
        private set;
    } = "CycleChallenge_LoadFailed".GetLocalized();

    #endregion


    public void Show(string payloadJson)
    {
        try
        {
            var dto = SpiralAbyssPayloadParser.Parse(payloadJson);
            Apply(dto);
        }
        catch (Exception ex)
        {
            DebugLog.Info("SpiralAbyssPanel", $"[SpiralAbyssPanel] Failed to parse payload: {ex}");
            ApplyError();
        }
    }


    public string? Region
    {
        get;
        set;
    }


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
        if (IsLoaded)
        {
            Bindings.Update();
        }
    }

    private void OnPanelLoaded(object sender, RoutedEventArgs e)
    {
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