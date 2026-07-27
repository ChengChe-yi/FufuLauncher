/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FufuLauncher.ViewModels;

public sealed class DcKpiTile
{
    public string Glyph { get; init; } = "";
    public string Title { get; init; } = string.Empty;
    public string Value { get; init; } = "-";
    public string Caption { get; init; } = string.Empty;
    public string ColorTag { get; init; } = "accent";
    public bool HasCaption => !string.IsNullOrEmpty(Caption);
}

public sealed class DcRateRow
{
    public string Name { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public double Rate { get; init; }
    public string RateText { get; init; } = string.Empty;
    public string ColorTag { get; init; } = "accent";
    public bool HasIcon => !string.IsNullOrEmpty(Icon);
}

public sealed class DcBar
{
    public string Label { get; init; } = string.Empty;
    public double Value { get; init; }
    public string ValueText { get; init; } = string.Empty;
    public string ColorTag { get; init; } = "accent";
    public bool IsHighlighted { get; init; }
}

public sealed class DcMoverRow
{
    public string Name { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public int Star { get; init; }
    public string CurrentText { get; init; } = string.Empty;
    public string PreviousText { get; init; } = string.Empty;
    public string ChangeText { get; init; } = string.Empty;
    public string ChangeGlyph { get; init; } = "";
    public string ChangeTag { get; init; } = "flat";
}

public sealed class DcTierMember
{
    public string Name { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public int Star { get; init; }
    public string UseRateText { get; init; } = string.Empty;
    public string DetailText { get; init; } = string.Empty;
    public string StarTag => Star == 5 ? "star5" : "star4";
}

public sealed class DcTierGroup
{
    public string RankName { get; init; } = string.Empty;
    public string TierTag { get; init; } = "f";
    public string CountText { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public List<DcTierMember> Members { get; init; } = new();
}

public sealed class DcRankRow
{
    public int Position { get; init; }
    public string PositionText => Position.ToString();
    public string Name { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public int Star { get; init; }
    public double UseRate { get; init; }
    public string UseRateText { get; init; } = string.Empty;
    public string OwnRateText { get; init; } = string.Empty;
    public string FieldShareText { get; init; } = string.Empty;
    public string ConstellationText { get; init; } = string.Empty;
    public string ClearTimeText { get; init; } = string.Empty;
    public bool HasClearTime { get; init; }
    public string ChangeText { get; init; } = string.Empty;
    public string ChangeGlyph { get; init; } = "";
    public string ChangeTag { get; init; } = "flat";
    public bool HasChange { get; init; }
    public string TierText { get; init; } = string.Empty;
    public string TierTag { get; init; } = "f";
    public string StarTag => Star == 5 ? "star5" : "star4";
}

public sealed class DcTeamMember
{
    public string? Avatar { get; init; }
    public string Name { get; init; } = string.Empty;
    public int Star { get; init; }
    public string StarTag => Star == 5 ? "star5" : "star4";
}

public sealed class DcTeamCard
{
    public int Position { get; init; }
    public string PositionText => "#" + Position;
    public List<DcTeamMember> Members { get; init; } = new();
    public string TeamNames { get; init; } = string.Empty;
    public double UseRate { get; init; }
    public string UseRateText { get; init; } = string.Empty;
    public string HasRateText { get; init; } = string.Empty;
    public string AttendRateText { get; init; } = string.Empty;
    public string UseCountText { get; init; } = string.Empty;
    public string ClearTimeText { get; init; } = string.Empty;
    public bool HasClearTime { get; init; }
    public List<DcBar> HalfSplit { get; init; } = new();
}

public sealed class DcNamedIcon
{
    public string Name { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public int Star { get; init; } = 5;
    public string StarTag => Star == 5 ? "star5" : "star4";
    public bool HasIcon => !string.IsNullOrEmpty(Icon);
}

public sealed class DcWishBanner
{
    public string? Avatar { get; init; }
    public string Version { get; init; } = string.Empty;
    public string TimeText { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
    public string StatusTag { get; init; } = "flat";
    public bool HasStatus => !string.IsNullOrEmpty(StatusText);
    public string RelativeText { get; init; } = string.Empty;
    public List<DcNamedIcon> Star5 { get; init; } = new();
    public List<DcNamedIcon> Star4 { get; init; } = new();
    public bool HasStar4 => Star4.Count > 0;
    public string SearchKey { get; init; } = string.Empty;
}

public sealed class DcRerunCard
{
    public string Name { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public int Star { get; init; }
    public string StarTag => Star == 5 ? "star5" : "star4";
    public string DaysText { get; init; } = string.Empty;
    public string DaysCaption { get; init; } = string.Empty;
    public string AvgDaysText { get; init; } = string.Empty;
    public string UpTimesText { get; init; } = string.Empty;
    public string MaxGapText { get; init; } = string.Empty;
    public string MinGapText { get; init; } = string.Empty;
    public string UrgencyText { get; init; } = string.Empty;
    public string UrgencyTag { get; init; } = "flat";
    public string ForecastText { get; init; } = string.Empty;
    public double ProgressValue { get; init; }
    public List<string> History { get; init; } = new();
    public bool HasHistory => History.Count > 0;
    public string HistoryText => string.Join(" · ", History);
    public double SortUrgency { get; init; }

    public double SortDays { get; init; }
    public double SortInterval { get; init; }
}

public sealed class DcCountRow
{
    public int Position { get; init; }
    public string PositionText => Position.ToString();
    public string Name { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public string CountText { get; init; } = string.Empty;
    public string DetailText { get; init; } = string.Empty;
    public double Ratio { get; init; }
    public bool HasIcon => !string.IsNullOrEmpty(Icon);
}

public sealed class DcInsight
{
    public string Glyph { get; init; } = "";
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string ColorTag { get; init; } = "accent";
}

public sealed class DcCharacterCard
{
    public string Name { get; init; } = string.Empty;
    public string Ename { get; init; } = string.Empty;
    public string? Avatar { get; init; }
    public int Star { get; init; }
    public string StarTag => Star == 5 ? "star5" : "star4";
    public string StarText { get; init; } = string.Empty;

    public double MetaScore { get; init; }
    public string MetaScoreText { get; init; } = string.Empty;
    public string TierText { get; init; } = string.Empty;
    public string TierTag { get; init; } = "f";

    public string LevelText { get; init; } = string.Empty;
    public string ConstellationText { get; init; } = string.Empty;
    public string TalentText { get; init; } = string.Empty;
    public string SampleText { get; init; } = string.Empty;

    public string DamageText { get; init; } = string.Empty;
    public string DamageName { get; init; } = string.Empty;

    public string AbyssRateText { get; init; } = string.Empty;
    public string StygianRateText { get; init; } = string.Empty;
    public string OwnRateText { get; init; } = string.Empty;

    public List<DcRateRow> TopWeapons { get; init; } = new();
    public List<DcRateRow> TopArtifacts { get; init; } = new();

    public string HeadlineText { get; init; } = string.Empty;
    public string HeadlineTag { get; init; } = "flat";
    public bool HasHeadline => !string.IsNullOrEmpty(HeadlineText);
    public double SortHeat { get; init; }
    public double SortOwn { get; init; }
    public double SortAbyss { get; init; }
    public double SortLevel { get; init; }
    public double SortDamage { get; init; }
    public double SortValue { get; init; }

    public string SearchKey { get; init; } = string.Empty;

    public DcCharacterDetail Detail { get; init; } = new();
}

public sealed class DcCharacterDetail
{
    public string Name { get; set; } = string.Empty;
    public string Ename { get; set; } = string.Empty;
    public string? Avatar { get; set; }
    public string StarText { get; set; } = string.Empty;
    public string StarTag { get; set; } = "star5";
    public string TierText { get; set; } = string.Empty;
    public string TierTag { get; set; } = "f";
    public string MetaScoreText { get; set; } = string.Empty;
    public string SubtitleText { get; set; } = string.Empty;

    public List<DcKpiTile> Metrics { get; set; } = new();
    public List<DcRateRow> Weapons { get; set; } = new();
    public List<DcRateRow> Artifacts { get; set; } = new();
    public List<DcBar> Constellations { get; set; } = new();
    public List<DcTeamCard> Teams { get; set; } = new();
    public List<DcNamedIcon> BannerHistory { get; set; } = new();
    public List<DcInsight> Insights { get; set; } = new();

    public string WishSummary { get; set; } = string.Empty;
    public string RerunSummary { get; set; } = string.Empty;

    public bool HasWeapons => Weapons.Count > 0;
    public bool HasArtifacts => Artifacts.Count > 0;
    public bool HasConstellations => Constellations.Count > 0;
    public bool HasTeams => Teams.Count > 0;
    public bool HasBannerHistory => BannerHistory.Count > 0;
    public bool HasInsights => Insights.Count > 0;
    public bool HasWishSummary => !string.IsNullOrEmpty(WishSummary);
    public bool HasRerunSummary => !string.IsNullOrEmpty(RerunSummary);
}

public sealed class DcOption
{
    public string Title { get; init; } = string.Empty;
    public string? Value { get; init; }
    public override string ToString() => Title;
}

public enum DataCenterView
{
    Overview = 0,
    Characters = 1,
    SpiralAbyss = 2,
    Stygian = 3,
    Wish = 4,
    Rerun = 5,
    Timeline = 6
}

public sealed class DcAbyssSection : INotifyPropertyChanged
{
    public bool IsStygian { get; init; }
    
    public bool ShowClearTime { get; init; }

    public List<DcOption> RankSortOptions { get; init; } = new();

    public ObservableCollection<DcKpiTile> Kpis { get; } = new();
    public ObservableCollection<DcTierGroup> Tiers { get; } = new();
    public ObservableCollection<DcRankRow> Ranks { get; } = new();
    public ObservableCollection<DcTeamCard> Teams { get; } = new();
    public ObservableCollection<DcMoverRow> Risers { get; } = new();
    public ObservableCollection<DcMoverRow> Fallers { get; } = new();
    public ObservableCollection<DcBar> RestartDistribution { get; } = new();
    public ObservableCollection<DcOption> Versions { get; } = new();
    public ObservableCollection<DcOption> TeamFilters { get; } = new();
    
    public List<DcTeamCard> AllTeams { get; } = new();

    public int TeamShown { get; set; }
    
    public string? LoadedVersion { get; set; }

    public string? LoadedTeamFilter { get; set; }

    private int _selectedVersionIndex = -1;

    public int SelectedVersionIndex
    {
        get => _selectedVersionIndex;
        set
        {
            if (_selectedVersionIndex == value) return;
            _selectedVersionIndex = value;
            Raise();
        }
    }

    private int _selectedTeamFilterIndex = -1;

    public int SelectedTeamFilterIndex
    {
        get => _selectedTeamFilterIndex;
        set
        {
            if (_selectedTeamFilterIndex == value) return;
            _selectedTeamFilterIndex = value;
            Raise();
        }
    }

    private int _subView;

    public bool ShowTier => _subView == 0;
    public bool ShowRank => _subView == 1;
    public bool ShowTeam => _subView == 2;

    public void SetSubView(int subView)
    {
        if (_subView == subView) return;
        _subView = subView;
        Raise(nameof(ShowTier));
        Raise(nameof(ShowRank));
        Raise(nameof(ShowTeam));
    }

    private string _rankSort = "use";

    public string RankSort
    {
        get => _rankSort;
        set
        {
            if (_rankSort == value) return;
            _rankSort = value;
            Raise();
        }
    }

    private string _headline = string.Empty;

    public string Headline
    {
        get => _headline;
        set
        {
            if (_headline == value) return;
            _headline = value;
            Raise();
        }
    }

    private string _tips = string.Empty;

    public string Tips
    {
        get => _tips;
        set
        {
            if (_tips == value) return;
            _tips = value;
            Raise();
            Raise(nameof(HasTips));
        }
    }

    public bool HasTips => !string.IsNullOrEmpty(_tips);

    private string _teamCountText = string.Empty;

    public string TeamCountText
    {
        get => _teamCountText;
        set
        {
            if (_teamCountText == value) return;
            _teamCountText = value;
            Raise();
        }
    }

    private string _teamMoreText = string.Empty;

    public string TeamMoreText
    {
        get => _teamMoreText;
        set
        {
            if (_teamMoreText == value) return;
            _teamMoreText = value;
            Raise();
        }
    }

    private bool _hasMoreTeams;

    public bool HasMoreTeams
    {
        get => _hasMoreTeams;
        set
        {
            if (_hasMoreTeams == value) return;
            _hasMoreTeams = value;
            Raise();
        }
    }

    private bool _showRestartDistribution;

    public bool ShowRestartDistribution
    {
        get => _showRestartDistribution;
        set
        {
            if (_showRestartDistribution == value) return;
            _showRestartDistribution = value;
            Raise();
        }
    }

    private bool _hasMovers;

    public bool HasMovers
    {
        get => _hasMovers;
        set
        {
            if (_hasMovers == value) return;
            _hasMovers = value;
            Raise();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Raise([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
