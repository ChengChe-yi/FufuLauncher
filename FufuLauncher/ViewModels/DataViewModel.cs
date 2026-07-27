/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using FufuLauncher.Helpers;
using FufuLauncher.Models.DataCenter;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public sealed class DataViewModel : INotifyPropertyChanged
{
    private const string Dash = "-";
    private const int CharacterPageSize = 24;
    private const int WishPageSize = 12;
    private const int TeamPageSize = 20;
    
    private const string GlyphInfo = "\uE946";
    private const string GlyphChart = "\uE9D9";
    private const string GlyphStarFill = "\uE735";
    private const string GlyphStar = "\uE734";
    private const string GlyphSync = "\uE895";
    private const string GlyphPeople = "\uE77B";
    private const string GlyphFlag = "\uE7C1";
    private const string GlyphBolt = "\uE945";
    private const string GlyphStopwatch = "\uE916";
    private const string GlyphLevel = "\uE9D2";
    private const string GlyphUp = "\uE74A";
    private const string GlyphDown = "\uE74B";
    private const string GlyphHistory = "\uE81C";
    private const string GlyphWeapon = "\uE7EF";
    private const string GlyphArtifact = "\uE9F9";
    private const string GlyphWarning = "\uE7BA";
    private const string GlyphCheck = "\uE73E";

    private readonly GameStatsService _stats;
    private readonly IDataCenterPdfReportService _pdfReport;

    private RoleAvgResponse? _roleAvg;
    
    private AbyssStatsBundle? _spiralLatest;
    private AbyssStatsBundle? _stygianLatest;
    
    private AbyssStatsBundle? _spiralView;
    private AbyssStatsBundle? _stygianView;

    private WishHistoryResponse? _wish;
    private RerunResponse? _rerun;
    
    private Dictionary<string, WishCharacterStat> _wishStats = new(StringComparer.Ordinal);

    private CancellationTokenSource? _cts;
    private bool _isInitialized;

    public DataViewModel(GameStatsService stats, IDataCenterPdfReportService pdfReport)
    {
        _stats = stats;
        _pdfReport = pdfReport;

        var rankSortOptions = new List<DcOption>
        {
            new() { Title = L("DataPage_UsageRate"), Value = "use" },
            new() { Title = L("DataPage_OwnRate"), Value = "own" },
            new() { Title = L("DataPage_ChangeVsLast"), Value = "change" },
            new() { Title = L("DataPage_SpeedrunTime"), Value = "time" }
        };

        Spiral = new DcAbyssSection { IsStygian = false, ShowClearTime = false, RankSortOptions = rankSortOptions };
        Stygian = new DcAbyssSection { IsStygian = true, ShowClearTime = true, RankSortOptions = rankSortOptions };

        CharacterSortOptions = new List<DcOption>
        {
            new() { Title = L("DataPage_SortHeat"), Value = "heat" },
            new() { Title = L("DataPage_SortAbyss"), Value = "abyss" },
            new() { Title = L("DataPage_SortValue"), Value = "value" },
            new() { Title = L("DataPage_SortOwn"), Value = "own" },
            new() { Title = L("DataPage_SortLevel"), Value = "level" },
            new() { Title = L("DataPage_SortDamage"), Value = "damage" }
        };

        StarFilterOptions = new List<DcOption>
        {
            new() { Title = L("DataPage_AllRoles"), Value = "0" },
            new() { Title = L("DataPage_Only5Star"), Value = "5" },
            new() { Title = L("DataPage_Only4Star"), Value = "4" }
        };

        RerunGroupOptions = new List<DcOption>
        {
            new() { Title = L("DataPage_RerunChar5"), Value = "0" },
            new() { Title = L("DataPage_RerunChar4"), Value = "1" },
            new() { Title = L("DataPage_RerunWeapon5"), Value = "2" },
            new() { Title = L("DataPage_RerunWeapon4"), Value = "3" }
        };

        RerunSortOptions = new List<DcOption>
        {
            new() { Title = L("DataPage_RerunSortUrgency"), Value = "urgency" },
            new() { Title = L("DataPage_RerunSortDays"), Value = "days" },
            new() { Title = L("DataPage_RerunSortInterval"), Value = "interval" }
        };
    }

    private DataCenterView _currentView = DataCenterView.Overview;

    public DataCenterView CurrentView
    {
        get => _currentView;
        private set
        {
            if (_currentView == value) return;
            _currentView = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsOverview));
            OnPropertyChanged(nameof(IsCharacters));
            OnPropertyChanged(nameof(IsSpiralAbyss));
            OnPropertyChanged(nameof(IsStygian));
            OnPropertyChanged(nameof(IsWish));
            OnPropertyChanged(nameof(IsRerun));
            OnPropertyChanged(nameof(IsTimeline));
            OnPropertyChanged(nameof(IsScrollContentVisible));
        }
    }

    public bool IsOverview => _currentView == DataCenterView.Overview;
    public bool IsCharacters => _currentView == DataCenterView.Characters;
    public bool IsSpiralAbyss => _currentView == DataCenterView.SpiralAbyss;
    public bool IsStygian => _currentView == DataCenterView.Stygian;
    public bool IsWish => _currentView == DataCenterView.Wish;
    public bool IsRerun => _currentView == DataCenterView.Rerun;
    public bool IsTimeline => _currentView == DataCenterView.Timeline;
    public bool IsScrollContentVisible => _currentView != DataCenterView.Timeline;
    public void SetView(DataCenterView view) => CurrentView = view;

    private bool _isLoading;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value) return;
            _isLoading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanExportPdf));
        }
    }

    private bool _isExporting;

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (_isExporting == value) return;
            _isExporting = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanExportPdf));
        }
    }

    public bool CanExportPdf => !IsLoading && !IsExporting && HasExportableData;

    private bool HasExportableData => OverviewKpis.Count > 0 || _allCharacters.Count > 0 ||
                                      Spiral.Ranks.Count > 0 || Stygian.Ranks.Count > 0;

    private bool _hasError;

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (_hasError == value) return;
            _hasError = value;
            OnPropertyChanged();
        }
    }

    private string _errorMessage = string.Empty;

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    private string _statusMessage = string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value) return;
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private string _dataSourceText = L("DataPage_DataProvidedBy");

    public string DataSourceText
    {
        get => _dataSourceText;
        private set
        {
            if (_dataSourceText == value) return;
            _dataSourceText = value;
            OnPropertyChanged();
        }
    }

    public ObservableCollection<DcKpiTile> OverviewKpis { get; } = new();
    public ObservableCollection<DcInsight> OverviewInsights { get; } = new();
    public ObservableCollection<DcMoverRow> OverviewRisers { get; } = new();
    public ObservableCollection<DcMoverRow> OverviewFallers { get; } = new();
    public ObservableCollection<DcRankRow> OverviewTopTier { get; } = new();
    public ObservableCollection<DcWishBanner> OverviewBanners { get; } = new();
    public ObservableCollection<DcCountRow> OverviewValuePicks { get; } = new();
    public ObservableCollection<DcRerunCard> OverviewOverdue { get; } = new();

    public bool HasOverviewBanners { get; private set; }
    public bool HasOverviewMovers { get; private set; }

    private readonly List<DcCharacterCard> _allCharacters = new();
    private List<DcCharacterCard> _filteredCharacters = new();

    public ObservableCollection<DcCharacterCard> Characters { get; } = new();

    public List<DcOption> CharacterSortOptions { get; }
    public List<DcOption> StarFilterOptions { get; }

    private string _characterSearch = string.Empty;
    private int _characterStarFilter;
    private string _characterSort = "heat";
    private int _characterShown;

    public string CharacterCountText { get; private set; } = string.Empty;
    public bool HasMoreCharacters { get; private set; }
    public bool HasNoCharacters { get; private set; }
    public string CharacterMoreText { get; private set; } = string.Empty;

    public void SearchCharacters(string? keyword)
    {
        _characterSearch = keyword?.Trim() ?? string.Empty;
        ApplyCharacterFilter();
    }

    public void SetCharacterStarFilter(int star)
    {
        _characterStarFilter = star;
        ApplyCharacterFilter();
    }

    public void SetCharacterSort(string? sort)
    {
        _characterSort = string.IsNullOrEmpty(sort) ? "heat" : sort;
        ApplyCharacterFilter();
    }

    public void ShowMoreCharacters()
    {
        _characterShown = Math.Min(_characterShown + CharacterPageSize, _filteredCharacters.Count);
        PushCharacterPage();
    }

    private void ApplyCharacterFilter()
    {
        IEnumerable<DcCharacterCard> query = _allCharacters;

        if (_characterStarFilter is 4 or 5)
        {
            query = query.Where(c => c.Star == _characterStarFilter);
        }

        if (!string.IsNullOrEmpty(_characterSearch))
        {
            query = query.Where(c => c.SearchKey.Contains(_characterSearch, StringComparison.OrdinalIgnoreCase));
        }

        query = _characterSort switch
        {
            "abyss" => query.OrderByDescending(c => c.SortAbyss).ThenByDescending(c => c.SortHeat),
            "value" => query.OrderByDescending(c => c.SortValue).ThenByDescending(c => c.SortAbyss),
            "own" => query.OrderByDescending(c => c.SortOwn).ThenByDescending(c => c.SortHeat),
            "level" => query.OrderByDescending(c => c.SortLevel).ThenByDescending(c => c.SortHeat),
            "damage" => query.OrderByDescending(c => c.SortDamage).ThenByDescending(c => c.SortHeat),
            _ => query.OrderByDescending(c => c.SortHeat).ThenByDescending(c => c.SortAbyss)
        };

        _filteredCharacters = query.ToList();
        _characterShown = Math.Min(CharacterPageSize, _filteredCharacters.Count);
        PushCharacterPage();
    }

    private void PushCharacterPage()
    {
        Characters.Clear();
        for (var i = 0; i < _characterShown; i++) Characters.Add(_filteredCharacters[i]);

        HasMoreCharacters = _characterShown < _filteredCharacters.Count;
        HasNoCharacters = _filteredCharacters.Count == 0;
        CharacterCountText = LF("DataPage_ShownCount", _characterShown, _filteredCharacters.Count);
        CharacterMoreText = LF("DataPage_ShowMore",
            Math.Min(CharacterPageSize, Math.Max(0, _filteredCharacters.Count - _characterShown)));

        OnPropertyChanged(nameof(HasMoreCharacters));
        OnPropertyChanged(nameof(HasNoCharacters));
        OnPropertyChanged(nameof(CharacterCountText));
        OnPropertyChanged(nameof(CharacterMoreText));
    }

    private DcCharacterDetail? _selectedCharacter;

    public DcCharacterDetail? SelectedCharacter
    {
        get => _selectedCharacter;
        private set
        {
            _selectedCharacter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCharacterTitle));
        }
    }

    public string SelectedCharacterTitle =>
        SelectedCharacter == null ? string.Empty : LF("DataPage_DetailTitle", SelectedCharacter.Name);

    public void SelectCharacter(DcCharacterCard? card) => SelectedCharacter = card?.Detail;

    public DcAbyssSection Spiral { get; }
    public DcAbyssSection Stygian { get; }

    public void SetAbyssSubView(DcAbyssSection? section, int subView) => section?.SetSubView(subView);

    public void SetAbyssRankSort(DcAbyssSection? section, string? sort)
    {
        if (section == null) return;
        section.RankSort = string.IsNullOrEmpty(sort) ? "use" : sort;

        var bundle = section.IsStygian ? _stygianView : _spiralView;
        if (bundle != null) BuildRanks(section, bundle);
    }

    public void ShowMoreTeams(DcAbyssSection? section)
    {
        if (section == null) return;
        section.TeamShown = Math.Min(section.TeamShown + TeamPageSize, section.AllTeams.Count);
        PushTeamPage(section);
    }

    private void PushTeamPage(DcAbyssSection section)
    {
        section.Teams.Clear();
        for (var i = 0; i < section.TeamShown && i < section.AllTeams.Count; i++)
        {
            section.Teams.Add(section.AllTeams[i]);
        }

        var shown = Math.Min(section.TeamShown, section.AllTeams.Count);
        section.TeamCountText = LF("DataPage_ShownCount", shown, section.AllTeams.Count);
        section.HasMoreTeams = shown < section.AllTeams.Count;
        section.TeamMoreText = LF("DataPage_ShowMore",
            Math.Min(TeamPageSize, Math.Max(0, section.AllTeams.Count - shown)));
    }

    private readonly List<DcWishBanner> _allCharacterBanners = new();
    private readonly List<DcWishBanner> _allWeaponBanners = new();
    private List<DcWishBanner> _filteredBanners = new();

    public ObservableCollection<DcWishBanner> WishBanners { get; } = new();
    public ObservableCollection<DcCountRow> WishTopReruns { get; } = new();
    public ObservableCollection<DcCountRow> WishTopCompanions { get; } = new();

    private bool _wishShowWeapons;
    private string _wishSearch = string.Empty;
    private int _wishShown;

    public bool WishShowWeapons => _wishShowWeapons;
    public bool WishShowCharacters => !_wishShowWeapons;
    public string WishCountText { get; private set; } = string.Empty;
    public bool WishHasMore { get; private set; }
    public bool WishHasNone { get; private set; }
    public string WishMoreText { get; private set; } = string.Empty;

    public void SetWishCategory(bool weapons)
    {
        if (_wishShowWeapons == weapons) return;
        _wishShowWeapons = weapons;
        OnPropertyChanged(nameof(WishShowWeapons));
        OnPropertyChanged(nameof(WishShowCharacters));
        ApplyWishFilter();
    }

    public void SearchWish(string? keyword)
    {
        _wishSearch = keyword?.Trim() ?? string.Empty;
        ApplyWishFilter();
    }

    public void ShowMoreWish()
    {
        _wishShown = Math.Min(_wishShown + WishPageSize, _filteredBanners.Count);
        PushWishPage();
    }

    private void ApplyWishFilter()
    {
        IEnumerable<DcWishBanner> query = _wishShowWeapons ? _allWeaponBanners : _allCharacterBanners;

        if (!string.IsNullOrEmpty(_wishSearch))
        {
            query = query.Where(b => b.SearchKey.Contains(_wishSearch, StringComparison.OrdinalIgnoreCase));
        }

        _filteredBanners = query.ToList();
        _wishShown = Math.Min(WishPageSize, _filteredBanners.Count);
        PushWishPage();
    }

    private void PushWishPage()
    {
        WishBanners.Clear();
        for (var i = 0; i < _wishShown; i++) WishBanners.Add(_filteredBanners[i]);

        WishHasMore = _wishShown < _filteredBanners.Count;
        WishHasNone = _filteredBanners.Count == 0;
        WishCountText = LF("DataPage_ShownCount", _wishShown, _filteredBanners.Count);
        WishMoreText = LF("DataPage_ShowMore",
            Math.Min(WishPageSize, Math.Max(0, _filteredBanners.Count - _wishShown)));

        OnPropertyChanged(nameof(WishHasMore));
        OnPropertyChanged(nameof(WishHasNone));
        OnPropertyChanged(nameof(WishCountText));
        OnPropertyChanged(nameof(WishMoreText));
    }

    private readonly List<List<DcRerunCard>> _rerunGroups = new();

    public ObservableCollection<DcRerunCard> RerunCards { get; } = new();
    public List<DcOption> RerunGroupOptions { get; }
    public List<DcOption> RerunSortOptions { get; }

    private int _rerunGroup;
    private string _rerunSort = "urgency";
    private string _rerunSearch = string.Empty;

    public string RerunCountText { get; private set; } = string.Empty;
    public bool RerunHasNone { get; private set; }

    public void SetRerunGroup(int group)
    {
        _rerunGroup = group;
        ApplyRerunFilter();
    }

    public void SetRerunSort(string? sort)
    {
        _rerunSort = string.IsNullOrEmpty(sort) ? "urgency" : sort;
        ApplyRerunFilter();
    }

    public void SearchRerun(string? keyword)
    {
        _rerunSearch = keyword?.Trim() ?? string.Empty;
        ApplyRerunFilter();
    }

    private void ApplyRerunFilter()
    {
        RerunCards.Clear();

        if (_rerunGroup >= 0 && _rerunGroup < _rerunGroups.Count)
        {
            IEnumerable<DcRerunCard> query = _rerunGroups[_rerunGroup];

            if (!string.IsNullOrEmpty(_rerunSearch))
            {
                query = query.Where(c => c.Name.Contains(_rerunSearch, StringComparison.OrdinalIgnoreCase));
            }
            
            query = _rerunSort switch
            {
                "days" => query.OrderByDescending(c => c.SortDays).ThenByDescending(c => c.SortUrgency),
                "interval" => query.OrderBy(c => c.SortInterval).ThenByDescending(c => c.SortUrgency),
                _ => query.OrderByDescending(c => c.SortUrgency).ThenByDescending(c => c.SortDays)
            };

            foreach (var card in query) RerunCards.Add(card);
        }

        RerunHasNone = RerunCards.Count == 0;
        RerunCountText = LF("DataPage_UnitEntries", RerunCards.Count);
        OnPropertyChanged(nameof(RerunHasNone));
        OnPropertyChanged(nameof(RerunCountText));
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadAllAsync(false);
    }

    public Task RefreshAsync() => LoadAllAsync(true);

    public async Task ExportPdfAsync(Microsoft.UI.Xaml.Window? owner)
    {
        if (!CanExportPdf) return;

        var path = await FilePickerService.PickSaveFileAsync(
            owner,
            new[] { (L("DataPage_ExportPdfFileType"), new[] { ".pdf" }) },
            $"FufuLauncher_DataCenter_{DateTime.Now:yyyyMMdd}",
            Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary,
            error => StatusMessage = LF("DataPage_ExportFailed", error));
        if (string.IsNullOrEmpty(path)) return;

        var previousStatus = StatusMessage;
        IsExporting = true;
        StatusMessage = L("DataPage_Exporting");

        try
        {
            var snapshot = CreateReportSnapshot(previousStatus);
            await _pdfReport.GenerateAsync(snapshot, path);
            StatusMessage = LF("DataPage_Exported", Path.GetFileName(path));
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DataViewModel] PDF 导出失败: {ex}");
            StatusMessage = LF("DataPage_ExportFailed", ex.Message);
        }
        finally
        {
            IsExporting = false;
            if (StatusMessage == L("DataPage_Exporting")) StatusMessage = previousStatus;
        }
    }

    private DataCenterReportSnapshot CreateReportSnapshot(string status)
    {
        return new DataCenterReportSnapshot(
            DateTimeOffset.Now,
            System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? Dash,
            DataSourceText,
            status,
            OverviewKpis.ToList(),
            OverviewInsights.ToList(),
            OverviewRisers.ToList(),
            OverviewFallers.ToList(),
            OverviewTopTier.ToList(),
            OverviewValuePicks.ToList(),
            OverviewBanners.ToList(),
            OverviewOverdue.ToList(),
            _allCharacters.ToList(),
            CreateAbyssSnapshot(Spiral),
            CreateAbyssSnapshot(Stygian),
            _allCharacterBanners.ToList(),
            _allWeaponBanners.ToList(),
            WishTopReruns.ToList(),
            WishTopCompanions.ToList(),
            _rerunGroups.Select(group => (IReadOnlyList<DcRerunCard>)group.ToList()).ToList());
    }

    private static DataCenterAbyssSnapshot CreateAbyssSnapshot(DcAbyssSection section) => new(
        section.Headline,
        section.LoadedVersion ?? Dash,
        section.Tips,
        section.ShowClearTime,
        section.Kpis.ToList(),
        section.Tiers.ToList(),
        section.Ranks.ToList(),
        section.AllTeams.ToList(),
        section.Risers.ToList(),
        section.Fallers.ToList(),
        section.RestartDistribution.ToList());

    private async Task LoadAllAsync(bool force)
    {
        if (IsLoading) return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        var token = _cts.Token;

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        StatusMessage = L("DataPage_Loading");

        try
        {
            var roleTask = _stats.GetRoleAveragesAsync(force, token);
            var spiralTask = _stats.GetSpiralAbyssAsync(null, null, force, token);
            var stygianTask = _stats.GetStygianAsync(null, null, force, token);
            var wishTask = _stats.GetWishHistoryAsync(force, token);
            var rerunTask = _stats.GetRerunListAsync(force, token);

            await Task.WhenAll(roleTask, spiralTask, stygianTask, wishTask, rerunTask);

            if (token.IsCancellationRequested) return;

            _roleAvg = roleTask.Result ?? _roleAvg;
            _spiralLatest = spiralTask.Result ?? _spiralLatest;
            _stygianLatest = stygianTask.Result ?? _stygianLatest;
            _wish = wishTask.Result ?? _wish;
            _rerun = rerunTask.Result ?? _rerun;

            _spiralView = _spiralLatest;
            _stygianView = _stygianLatest;

            var loaded = new[]
            {
                _roleAvg != null, _spiralLatest != null, _stygianLatest != null, _wish != null, _rerun != null
            }.Count(ok => ok);

            if (loaded == 0)
            {
                HasError = true;
                ErrorMessage = L("DataPage_LoadFailedBody");
                StatusMessage = string.Empty;
                return;
            }

            RebuildAll();

            if (loaded < 5)
            {
                StatusMessage = LF("DataPage_PartialData", 5 - loaded);
            }
        }
        catch (OperationCanceledException) {}
        catch (Exception ex)
        {
            Debug.WriteLine($"[DataViewModel] 加载失败: {ex}");
            HasError = _allCharacters.Count == 0 && OverviewKpis.Count == 0;
            ErrorMessage = L("DataPage_LoadFailedBody");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    public async Task ChangeAbyssVersionAsync(DcAbyssSection? section, string? version)
    {
        if (section == null || IsLoading) return;
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));

        IsLoading = true;
        try
        {
            var bundle = section.IsStygian
                ? await _stats.GetStygianAsync(version, null, false, cts.Token)
                : await _stats.GetSpiralAbyssAsync(version, null, false, cts.Token);

            if (bundle == null) return;

            if (section.IsStygian) _stygianView = bundle;
            else _spiralView = bundle;

            BuildAbyssSection(section, bundle);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DataViewModel] 切换期数失败: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    
    public async Task ChangeTeamFilterAsync(DcAbyssSection? section, string? role)
    {
        if (section == null || IsLoading) return;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(40));

        IsLoading = true;
        try
        {
            var bundle = section.IsStygian
                ? await _stats.GetStygianAsync(null, role, false, cts.Token)
                : await _stats.GetSpiralAbyssAsync(null, role, false, cts.Token);

            if (bundle == null) return;

            BuildTeams(section, bundle);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DataViewModel] 切换配队筛选失败: {ex}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void RebuildAll()
    {
        if (_spiralView != null) BuildAbyssSection(Spiral, _spiralView);
        if (_stygianView != null) BuildAbyssSection(Stygian, _stygianView);

        _wishStats = BuildWishStats();

        BuildWish();
        BuildRerun();
        BuildCharacters();
        BuildOverview();
        OnPropertyChanged(nameof(CanExportPdf));

        var parts = new List<string>();
        if (_spiralLatest?.Response.Version is { Length: > 0 } version) parts.Add(version);
        if (_spiralLatest?.Response.LastUpdate is { Length: > 0 } updated)
        {
            parts.Add(LF("DataPage_LastUpdate", updated));
        }

        StatusMessage = string.Join(" · ", parts);

        if (_roleAvg?.DataFrom is { Length: > 0 } disclaimer)
        {
            DataSourceText = disclaimer + "  |  " + L("DataPage_DataProvidedBy");
        }
    }

    private void BuildCharacters()
    {
        _allCharacters.Clear();
        if (_roleAvg?.Result == null)
        {
            ApplyCharacterFilter();
            return;
        }

        var spiralRanks = IndexRanks(_spiralLatest);
        var stygianRanks = IndexRanks(_stygianLatest);
        var spiralTiers = IndexTiers(_spiralLatest);
        var stygianTiers = IndexTiers(_stygianLatest);
        var rerunIndex = IndexRerun();

        foreach (var entry in _roleAvg.Result)
        {
            var name = entry.Role;
            if (string.IsNullOrEmpty(name)) continue;

            spiralRanks.TryGetValue(name, out var spiralRank);
            stygianRanks.TryGetValue(name, out var stygianRank);
            spiralTiers.TryGetValue(name, out var spiralTier);
            stygianTiers.TryGetValue(name, out var stygianTier);

            var abyssPick = spiralRank?.UseRate ?? 0;
            var stygianPick = stygianRank?.UseRate ?? 0;
            var ownRate = spiralRank?.OwnRate ?? stygianRank?.OwnRate ?? 0;
            
            var fieldShare = abyssPick * ownRate / 100d;

            var score = Math.Clamp(
                0.40 * abyssPick + 0.25 * stygianPick + 0.20 * fieldShare + 0.15 * ownRate, 0, 100);
            var (tierText, tierTag) = ScoreToTier(score);
            
            var valueIndex = abyssPick - ownRate;

            var star = entry.Star ?? 5;
            var (headline, headlineTag) = BuildHeadline(score, valueIndex, ownRate, spiralRank);

            var detail = BuildCharacterDetail(entry, spiralRank, stygianRank, spiralTier ?? stygianTier,
                score, tierText, tierTag, rerunIndex);

            _allCharacters.Add(new DcCharacterCard
            {
                Name = name!,
                Ename = entry.Ename ?? string.Empty,
                Avatar = entry.Avatar,
                Star = star,
                StarText = star + L("DataPage_StarUnit"),
                MetaScore = score,
                MetaScoreText = score.ToString("0", CultureInfo.InvariantCulture),
                TierText = tierText,
                TierTag = tierTag,
                LevelText = "Lv." + Fmt(entry.AvgLevel, 1),
                ConstellationText = "C" + Fmt(entry.AvgConstellation, 2),
                TalentText = $"{Fmt(entry.Ability1, 1)} / {Fmt(entry.Ability2, 1)} / {Fmt(entry.Ability3, 1)}",
                SampleText = LF("DataPage_SampleFormat", Compact(entry.RoleSum)),
                DamageText = NumText(entry.Damage),
                DamageName = entry.DamageName ?? string.Empty,
                AbyssRateText = PctText(spiralRank?.UseRate),
                StygianRateText = PctText(stygianRank?.UseRate),
                OwnRateText = PctText(ownRate > 0 ? ownRate : null),
                TopWeapons = BuildWeaponRows(entry.Weapons, 3),
                TopArtifacts = BuildArtifactRows(entry.ArtifactSets, 3),
                HeadlineText = headline,
                HeadlineTag = headlineTag,
                SortHeat = score,
                SortOwn = ownRate,
                SortAbyss = abyssPick,
                SortLevel = entry.AvgLevel ?? 0,
                SortDamage = entry.Damage ?? 0,
                SortValue = valueIndex,
                SearchKey = string.Join(' ', name, entry.Ename, entry.DamageName),
                Detail = detail
            });
        }

        ApplyCharacterFilter();
    }

    private static (string text, string tag) BuildHeadline(double score, double valueIndex, double ownRate,
        AbyssRankEntry? spiralRank)
    {
        if (score >= 78) return (L("DataPage_HeadlineTop"), "s1");
        if (valueIndex >= 20) return (L("DataPage_HeadlineValue"), "up");
        if (spiralRank?.UseRateChange is >= 15) return (L("DataPage_HeadlineRising"), "up");
        if (spiralRank?.UseRateChange is <= -15) return (L("DataPage_HeadlineFalling"), "down");
        if (ownRate >= 95) return (L("DataPage_HeadlinePopular"), "a");
        return (string.Empty, "flat");
    }

    private DcCharacterDetail BuildCharacterDetail(RoleAvgEntry entry, AbyssRankEntry? spiralRank,
        AbyssRankEntry? stygianRank, AbyssTierEntry? tierEntry, double score, string tierText, string tierTag,
        Dictionary<string, RerunEntry> rerunIndex)
    {
        var name = entry.Role ?? string.Empty;
        var star = entry.Star ?? 5;
        var ownRate = spiralRank?.OwnRate ?? stygianRank?.OwnRate;

        var detail = new DcCharacterDetail
        {
            Name = name,
            Ename = entry.Ename ?? string.Empty,
            Avatar = entry.Avatar,
            StarText = star + L("DataPage_StarUnit"),
            StarTag = star == 5 ? "star5" : "star4",
            TierText = tierText,
            TierTag = tierTag,
            MetaScoreText = score.ToString("0", CultureInfo.InvariantCulture),
            SubtitleText = string.IsNullOrEmpty(entry.DamageName)
                ? entry.Ename ?? string.Empty
                : $"{entry.Ename} · {entry.DamageName}"
        };

        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphBolt, Title = L("DataPage_AbyssPick"), Value = PctText(spiralRank?.UseRate),
            Caption = RankClassText(spiralRank?.RankClass), ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphFlag, Title = L("DataPage_StygianPick"), Value = PctText(stygianRank?.UseRate),
            Caption = RankClassText(stygianRank?.RankClass), ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphPeople, Title = L("DataPage_OwnRate"), Value = PctText(ownRate),
            Caption = L("DataPage_FieldShare") + " " +
                      PctText((spiralRank?.UseRate ?? 0) * (ownRate ?? 0) / 100d),
            ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphLevel, Title = L("DataPage_MetricAvgLevel"), Value = "Lv." + Fmt(entry.AvgLevel, 1),
            Caption = L("DataPage_MetricTalent") +
                      $" {Fmt(entry.Ability1, 1)}/{Fmt(entry.Ability2, 1)}/{Fmt(entry.Ability3, 1)}",
            ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphStar, Title = L("DataPage_MetricAvgConst"), Value = "C" + Fmt(entry.AvgConstellation, 2),
            Caption = L("DataPage_ZeroConstellation") + " " + PctText(tierEntry?.C0Rate ?? entry.C0), ColorTag = "accent"
        });
        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphBolt, Title = L("DataPage_CoreDamage"), Value = NumText(entry.Damage),
            Caption = entry.DamageName ?? string.Empty, ColorTag = "accent"
        });

        if (stygianRank?.ClearTime is > 0)
        {
            detail.Metrics.Add(new DcKpiTile
            {
                Glyph = GlyphStopwatch, Title = L("DataPage_ClearTime"),
                Value = Fmt(stygianRank.ClearTime, 1) + "s", Caption = L("DataPage_TabStygian"), ColorTag = "accent"
            });
        }

        detail.Metrics.Add(new DcKpiTile
        {
            Glyph = GlyphChart, Title = L("DataPage_MetricSample"), Value = Compact(entry.RoleSum),
            Caption = CleanVersion(_roleAvg?.Version), ColorTag = "accent"
        });

        detail.Weapons = BuildWeaponRows(entry.Weapons, 6);
        detail.Artifacts = BuildArtifactRows(entry.ArtifactSets, 6);
        
        var constellations = tierEntry != null
            ? new[]
            {
                tierEntry.C0Rate, tierEntry.C1Rate, tierEntry.C2Rate, tierEntry.C3Rate, tierEntry.C4Rate,
                tierEntry.C5Rate, tierEntry.C6Rate
            }
            : new[] { entry.C0, entry.C1, entry.C2, entry.C3, entry.C4, entry.C5, entry.C6 };

        var maxConst = constellations.Max(c => c ?? 0);
        for (var i = 0; i < constellations.Length; i++)
        {
            var value = constellations[i] ?? 0;
            detail.Constellations.Add(new DcBar
            {
                Label = "C" + i,
                Value = value,
                ValueText = PctText(value),
                ColorTag = i == 0 ? "accent" : "muted",
                IsHighlighted = value > 0 && Math.Abs(value - maxConst) < 0.001
            });
        }

        detail.Teams = FindTeamsFor(entry.Avatar, name, 4);

        if (_wishStats.TryGetValue(name, out var stat) && stat.Count > 0)
        {
            foreach (var version in stat.Banners.Take(12))
            {
                detail.BannerHistory.Add(new DcNamedIcon { Name = version, Star = star });
            }

            detail.WishSummary = LF("DataPage_WishSummaryFormat", stat.Count, stat.LatestVersion,
                stat.DaysSince?.ToString("0", CultureInfo.InvariantCulture) ?? Dash,
                stat.AverageGap?.ToString("0", CultureInfo.InvariantCulture) ?? Dash);
        }
        else
        {
            detail.WishSummary = L("DataPage_WishSummaryNever");
        }

        if (rerunIndex.TryGetValue(name, out var rerun))
        {
            detail.RerunSummary = LF("DataPage_RerunSummaryFormat", Fmt(rerun.Days, 0), Fmt(rerun.AvgDays, 0),
                Fmt(rerun.MaxGapDays, 0));
        }

        BuildCharacterInsights(detail, entry, spiralRank, ownRate, tierEntry, rerunIndex.GetValueOrDefault(name));

        return detail;
    }
    
    private static void BuildCharacterInsights(DcCharacterDetail detail, RoleAvgEntry entry,
        AbyssRankEntry? spiralRank, double? ownRateValue, AbyssTierEntry? tierEntry, RerunEntry? rerun)
    {
        var name = entry.Role ?? string.Empty;
        var abyssPick = spiralRank?.UseRate ?? 0;
        var ownRate = ownRateValue ?? 0;

        if (abyssPick >= 60 && ownRate > 0 && abyssPick - ownRate >= 15)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphStarFill, ColorTag = "up", Title = L("DataPage_InsightValueTitle"),
                Body = LF("DataPage_InsightValueBody", name, PctText(abyssPick), PctText(ownRate))
            });
        }
        else if (ownRate >= 95 && abyssPick is > 0 and < 30)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphWarning, ColorTag = "down", Title = L("DataPage_InsightBenchTitle"),
                Body = LF("DataPage_InsightBenchBody", name, PctText(ownRate), PctText(abyssPick))
            });
        }

        if (spiralRank is { UseRateChange: { } change, UseRateOld: not null } && Math.Abs(change) >= 8)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = change > 0 ? GlyphUp : GlyphDown,
                ColorTag = change > 0 ? "up" : "down",
                Title = change > 0 ? L("DataPage_InsightRiseTitle") : L("DataPage_InsightFallTitle"),
                Body = LF(change > 0 ? "DataPage_InsightRiseBody" : "DataPage_InsightFallBody",
                    name, PctText(spiralRank.UseRateOld), PctText(spiralRank.UseRate))
            });
        }

        var zeroConst = tierEntry?.C0Rate ?? entry.C0;
        if (zeroConst is >= 70)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphCheck, ColorTag = "up", Title = L("DataPage_InsightNoConstTitle"),
                Body = LF("DataPage_InsightNoConstBody", name, PctText(zeroConst))
            });
        }
        else if (zeroConst is > 0 and < 40)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphWarning, ColorTag = "down", Title = L("DataPage_InsightConstTitle"),
                Body = LF("DataPage_InsightConstBody", name, PctText(zeroConst))
            });
        }
        
        var topWeapon = entry.Weapons?.FirstOrDefault();
        if (topWeapon is { Rate: >= 50, Name: { Length: > 0 } weaponName })
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphWeapon, ColorTag = "accent", Title = L("DataPage_InsightWeaponTitle"),
                Body = LF("DataPage_InsightWeaponBody", weaponName, PctText(topWeapon.Rate), name)
            });
        }

        var topArtifact = entry.ArtifactSets?.FirstOrDefault();
        if (topArtifact is { Rate: >= 50, Name: { Length: > 0 } artifactName })
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphArtifact, ColorTag = "accent", Title = L("DataPage_InsightArtifactTitle"),
                Body = LF("DataPage_InsightArtifactBody", artifactName, PctText(topArtifact.Rate))
            });
        }

        if (rerun is { Days: { } days, AvgDays: > 0 } && days >= rerun.AvgDays!.Value)
        {
            detail.Insights.Add(new DcInsight
            {
                Glyph = GlyphHistory, ColorTag = "up", Title = L("DataPage_InsightOverdueTitle"),
                Body = LF("DataPage_InsightOverdueBody", name, Fmt(days, 0), Fmt(rerun.AvgDays, 0))
            });
        }
    }

    private void BuildAbyssSection(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        var response = bundle.Response;

        section.Kpis.Clear();
        section.Kpis.Add(new DcKpiTile
        {
            Glyph = GlyphChart, Title = L("DataPage_KpiSample"), Value = Compact(response.SampleCount),
            Caption = response.UpdateInfo ?? string.Empty, ColorTag = "accent"
        });
        section.Kpis.Add(new DcKpiTile
        {
            Glyph = GlyphStarFill, Title = L("DataPage_KpiFullStar"), Value = response.FullStarRate ?? Dash,
            Caption = L("DataPage_KpiOnceFullStar") + " " + (response.FullStarOnceRate ?? Dash), ColorTag = "up"
        });
        section.Kpis.Add(new DcKpiTile
        {
            Glyph = GlyphSync, Title = L("DataPage_KpiRestart"), Value = Fmt(response.RestartTimesAvg, 1),
            Caption = L("DataPage_UnitTimesShort"), ColorTag = "accent"
        });
        section.Kpis.Add(new DcKpiTile
        {
            Glyph = GlyphPeople, Title = L("DataPage_KpiCharacterCount"),
            Value = (response.HasList?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
            Caption = CleanVersion(response.Version), ColorTag = "accent"
        });

        section.RestartDistribution.Clear();
        foreach (var item in response.RestartInfo ?? new List<AbyssRestartEntry>())
        {
            section.RestartDistribution.Add(new DcBar
            {
                Label = item.Intro ?? string.Empty,
                Value = item.Rate ?? 0,
                ValueText = PctText(item.Rate),
                ColorTag = "accent"
            });
        }
        
        if (section.RestartDistribution.All(bar => bar.Value <= 0)) section.RestartDistribution.Clear();
        section.ShowRestartDistribution = section.RestartDistribution.Count > 0;

        BuildTiers(section, bundle);
        BuildRanks(section, bundle);
        BuildTeams(section, bundle);
        BuildMovers(section, bundle);
        
        if (section.Versions.Count == 0)
        {
            foreach (var option in response.HistoryList ?? new List<AbyssOption>())
            {
                section.Versions.Add(new DcOption { Title = option.Title ?? string.Empty, Value = option.Value });
            }

            if (section.Versions.Count > 0)
            {
                section.LoadedVersion = section.Versions[0].Value;
                section.SelectedVersionIndex = 0;
            }
        }

        if (section.TeamFilters.Count == 0)
        {
            foreach (var option in response.SelectList ?? new List<AbyssOption>())
            {
                section.TeamFilters.Add(new DcOption { Title = option.Title ?? string.Empty, Value = option.Value });
            }

            if (section.TeamFilters.Count > 0)
            {
                section.LoadedTeamFilter = section.TeamFilters[0].Value;
                section.SelectedTeamFilterIndex = 0;
            }
        }

        section.Headline = response.Title ?? string.Empty;
        section.Tips = string.Join(" ",
            new[] { response.Tips, response.Tips2 }.Where(text => !string.IsNullOrEmpty(text)));
    }

    private static void BuildTiers(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        section.Tiers.Clear();

        foreach (var group in bundle.Tiers)
        {
            var members = new List<DcTierMember>();

            foreach (var item in (group.List ?? new List<AbyssTierEntry>()).OrderByDescending(x => x.UseRate ?? 0))
            {
                var parts = new List<string> { L("DataPage_OwnRate") + " " + PctText(item.OwnRate) };
                if (item.C0Rate.HasValue) parts.Add("C0 " + PctText(item.C0Rate));
                if (item.ClearTime is > 0) parts.Add(Fmt(item.ClearTime, 0) + "s");

                members.Add(new DcTierMember
                {
                    Name = item.Name ?? string.Empty,
                    Avatar = item.Avatar,
                    Star = item.Star ?? 5,
                    UseRateText = PctText(item.UseRate),
                    DetailText = string.Join(" · ", parts)
                });
            }

            if (members.Count == 0) continue;

            section.Tiers.Add(new DcTierGroup
            {
                RankName = group.RankName ?? string.Empty,
                TierTag = NormalizeTierTag(group.RankClass),
                CountText = LF("DataPage_TierCount", members.Count),
                Description = TierDescription(group.RankClass),
                Members = members
            });
        }
    }

    private static void BuildRanks(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        section.Ranks.Clear();

        IEnumerable<AbyssRankEntry> query = bundle.Ranks;

        query = section.RankSort switch
        {
            "own" => query.OrderByDescending(x => x.OwnRate ?? 0),
            "change" => query.OrderByDescending(x => x.UseRateChange ?? double.MinValue),
            "time" => query.Where(x => x.ClearTime is > 0).OrderBy(x => x.ClearTime),
            _ => query.OrderByDescending(x => x.UseRate ?? 0)
        };

        var position = 0;
        foreach (var item in query)
        {
            position++;
            var change = item.UseRateChange;
            var hasChange = change.HasValue && item.UseRateOld.HasValue && Math.Abs(change.Value) > 0.05;

            section.Ranks.Add(new DcRankRow
            {
                Position = position,
                Name = item.Name ?? string.Empty,
                Avatar = item.Avatar,
                Star = item.Star ?? 5,
                UseRate = item.UseRate ?? 0,
                UseRateText = PctText(item.UseRate),
                OwnRateText = PctText(item.OwnRate),
                FieldShareText = PctText((item.UseRate ?? 0) * (item.OwnRate ?? 0) / 100d),
                ConstellationText = item.AvgConstellation.HasValue ? "C" + Fmt(item.AvgConstellation, 1) : Dash,
                ClearTimeText = item.ClearTime is > 0 ? Fmt(item.ClearTime, 1) + "s" : Dash,
                HasClearTime = section.ShowClearTime,
                HasChange = hasChange,
                ChangeText = hasChange ? SignedPct(change) : Dash,
                ChangeGlyph = hasChange ? change > 0 ? GlyphUp : GlyphDown : string.Empty,
                ChangeTag = !hasChange ? "flat" : change > 0 ? "up" : "down",
                TierText = RankClassText(item.RankClass),
                TierTag = NormalizeTierTag(item.RankClass)
            });
        }
    }

    private void BuildTeams(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        section.AllTeams.Clear();

        var position = 0;
        foreach (var team in bundle.Teams.OrderByDescending(t => t.UseRate ?? 0))
        {
            var members = new List<DcTeamMember>();
            foreach (var member in team.Members ?? new List<AbyssTeamMember>())
            {
                members.Add(new DcTeamMember
                {
                    Avatar = member.Avatar,
                    Star = member.Star ?? 5,
                    Name = bundle.ResolveName(member.Avatar) ?? string.Empty
                });
            }

            if (members.Count == 0) continue;
            position++;

            var halves = new List<DcBar>();
            AddHalf(halves, "DataPage_HalfFirst", team.FirstHalfRate);
            AddHalf(halves, "DataPage_HalfMid", team.MidHalfRate);
            AddHalf(halves, "DataPage_HalfSecond", team.SecondHalfRate);

            section.AllTeams.Add(new DcTeamCard
            {
                Position = position,
                Members = members,
                TeamNames = string.Join(" · ", members.Select(m => m.Name).Where(n => !string.IsNullOrEmpty(n))),
                UseRate = team.UseRate ?? 0,
                UseRateText = PctText(team.UseRate),
                HasRateText = PctText(team.HasRate),
                AttendRateText = PctText(team.AttendRate),
                UseCountText = LF("DataPage_TeamCount", Compact(team.UseCount)),
                ClearTimeText = team.ClearTime is > 0 ? Fmt(team.ClearTime, 1) + "s" : Dash,
                HasClearTime = team.ClearTime is > 0,
                HalfSplit = halves
            });
        }

        section.TeamShown = Math.Min(TeamPageSize, section.AllTeams.Count);
        PushTeamPage(section);
    }

    private static void AddHalf(List<DcBar> target, string key, double? rate)
    {
        if (rate is not > 0) return;
        target.Add(new DcBar { Label = L(key), Value = rate.Value, ValueText = PctText(rate), ColorTag = "accent" });
    }

    private static void BuildMovers(DcAbyssSection section, AbyssStatsBundle bundle)
    {
        section.Risers.Clear();
        section.Fallers.Clear();

        var withChange = bundle.Ranks
            .Where(r => r.UseRateChange.HasValue && r.UseRateOld.HasValue && Math.Abs(r.UseRateChange!.Value) > 0.05)
            .ToList();

        foreach (var item in withChange.OrderByDescending(r => r.UseRateChange).Take(5))
        {
            section.Risers.Add(ToMover(item));
        }

        foreach (var item in withChange.OrderBy(r => r.UseRateChange).Take(5))
        {
            section.Fallers.Add(ToMover(item));
        }

        section.HasMovers = section.Risers.Count > 0 || section.Fallers.Count > 0;
    }

    private static DcMoverRow ToMover(AbyssRankEntry item)
    {
        var change = item.UseRateChange ?? 0;
        return new DcMoverRow
        {
            Name = item.Name ?? string.Empty,
            Avatar = item.Avatar,
            Star = item.Star ?? 5,
            CurrentText = PctText(item.UseRate),
            PreviousText = PctText(item.UseRateOld),
            ChangeText = SignedPct(change),
            ChangeGlyph = change > 0 ? GlyphUp : GlyphDown,
            ChangeTag = change > 0 ? "up" : "down"
        };
    }

    private sealed class WishCharacterStat
    {
        public int Count { get; set; }
        public string LatestVersion { get; set; } = Dash;
        public DateTime? LatestDate { get; set; }
        public double? DaysSince { get; set; }
        public double? AverageGap { get; set; }
        public List<string> Banners { get; } = new();
        public List<DateTime> Dates { get; } = new();
    }

    private Dictionary<string, WishCharacterStat> BuildWishStats()
    {
        var result = new Dictionary<string, WishCharacterStat>(StringComparer.Ordinal);
        if (_wish?.Characters == null) return result;
        
        foreach (var banner in _wish.Characters.AsEnumerable().Reverse())
        {
            var (start, _) = ParseRange(banner.Time);

            foreach (var name in banner.Star5 ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;

                if (!result.TryGetValue(name, out var stat))
                {
                    stat = new WishCharacterStat();
                    result[name] = stat;
                }

                stat.Count++;
                stat.Banners.Insert(0, banner.Version ?? string.Empty);
                if (start.HasValue)
                {
                    stat.Dates.Add(start.Value);
                    stat.LatestDate = start;
                }

                stat.LatestVersion = banner.Version ?? stat.LatestVersion;
            }
        }

        var today = DateTime.Today;
        foreach (var stat in result.Values)
        {
            if (stat.LatestDate.HasValue) stat.DaysSince = (today - stat.LatestDate.Value).TotalDays;

            if (stat.Dates.Count >= 2)
            {
                var ordered = stat.Dates.OrderBy(d => d).ToList();
                var gaps = new List<double>();
                for (var i = 1; i < ordered.Count; i++) gaps.Add((ordered[i] - ordered[i - 1]).TotalDays);
                stat.AverageGap = gaps.Average();
            }
        }

        return result;
    }

    private void BuildWish()
    {
        _allCharacterBanners.Clear();
        _allWeaponBanners.Clear();
        WishTopReruns.Clear();
        WishTopCompanions.Clear();

        if (_wish == null)
        {
            ApplyWishFilter();
            return;
        }

        var icons = _wish.AvatarList ?? new Dictionary<string, string>();

        foreach (var banner in _wish.Characters ?? new List<WishBannerEntry>())
        {
            _allCharacterBanners.Add(ToBanner(banner, icons));
        }

        foreach (var banner in _wish.Weapons ?? new List<WishBannerEntry>())
        {
            _allWeaponBanners.Add(ToBanner(banner, icons));
        }
        
        var maxCount = _wishStats.Values.Select(s => s.Count).DefaultIfEmpty(1).Max();
        var position = 0;
        foreach (var pair in _wishStats.OrderByDescending(p => p.Value.Count).ThenBy(p => p.Key, StringComparer.Ordinal)
                     .Take(10))
        {
            position++;
            WishTopReruns.Add(new DcCountRow
            {
                Position = position,
                Name = pair.Key,
                Icon = icons.GetValueOrDefault(pair.Key),
                CountText = LF("DataPage_WishUpTimes", pair.Value.Count),
                DetailText = pair.Value.DaysSince.HasValue
                    ? LF("DataPage_WishDaysAgo", pair.Value.DaysSince.Value.ToString("0", CultureInfo.InvariantCulture))
                    : pair.Value.LatestVersion,
                Ratio = maxCount > 0 ? pair.Value.Count * 100d / maxCount : 0
            });
        }
        
        var companions = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var banner in _wish.Characters ?? new List<WishBannerEntry>())
        {
            foreach (var name in banner.Star4 ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                companions[name] = companions.GetValueOrDefault(name) + 1;
            }
        }

        var maxCompanion = companions.Values.DefaultIfEmpty(1).Max();
        position = 0;
        foreach (var pair in companions.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
                     .Take(10))
        {
            position++;
            WishTopCompanions.Add(new DcCountRow
            {
                Position = position,
                Name = pair.Key,
                Icon = icons.GetValueOrDefault(pair.Key),
                CountText = LF("DataPage_WishUpTimes", pair.Value),
                Ratio = maxCompanion > 0 ? pair.Value * 100d / maxCompanion : 0
            });
        }

        ApplyWishFilter();
    }

    private static DcWishBanner ToBanner(WishBannerEntry banner, Dictionary<string, string> icons)
    {
        var (start, end) = ParseRange(banner.Time);
        var today = DateTime.Today;

        var status = string.Empty;
        var statusTag = "flat";
        var relative = string.Empty;

        if (start.HasValue && end.HasValue)
        {
            if (today >= start.Value && today <= end.Value)
            {
                status = L("DataPage_WishOngoing");
                statusTag = "up";
                relative = LF("DataPage_WishDaysLeft",
                    Math.Max(0, (end.Value - today).TotalDays).ToString("0", CultureInfo.InvariantCulture));
            }
            else if (start.Value > today)
            {
                status = L("DataPage_WishUpcoming");
                statusTag = "accent";
                relative = LF("DataPage_WishDaysUntil",
                    (start.Value - today).TotalDays.ToString("0", CultureInfo.InvariantCulture));
            }
            else
            {
                relative = LF("DataPage_WishDaysAgo",
                    (today - start.Value).TotalDays.ToString("0", CultureInfo.InvariantCulture));
            }
        }

        var star5 = (banner.Star5 ?? new List<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => new DcNamedIcon { Name = n, Icon = icons.GetValueOrDefault(n), Star = 5 })
            .ToList();

        var star4 = (banner.Star4 ?? new List<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => new DcNamedIcon { Name = n, Icon = icons.GetValueOrDefault(n), Star = 4 })
            .ToList();

        return new DcWishBanner
        {
            Avatar = banner.Avatar,
            Version = banner.Version ?? string.Empty,
            TimeText = banner.Time ?? string.Empty,
            StatusText = status,
            StatusTag = statusTag,
            RelativeText = relative,
            Star5 = star5,
            Star4 = star4,
            SearchKey = string.Join(' ',
                new[] { banner.Version ?? string.Empty }
                    .Concat(star5.Select(s => s.Name))
                    .Concat(star4.Select(s => s.Name)))
        };
    }

    private void BuildRerun()
    {
        _rerunGroups.Clear();

        foreach (var group in _rerun?.Result ?? new List<List<RerunEntry>>())
        {
            var cards = new List<DcRerunCard>();

            foreach (var entry in group)
            {
                if (string.IsNullOrEmpty(entry.Name)) continue;

                var days = entry.Days ?? 0;
                var avg = entry.AvgDays ?? 0;
                var ratio = avg > 0 ? days / avg : 0;

                var (urgencyText, urgencyTag) = ratio switch
                {
                    >= 1.5 => (L("DataPage_RerunOverdue"), "overdue"),
                    >= 1.0 => (L("DataPage_RerunDue"), "due"),
                    >= 0.7 => (L("DataPage_RerunSoon"), "soon"),
                    > 0 => (L("DataPage_RerunFresh"), "fresh"),
                    _ => (L("DataPage_RerunNever"), "flat")
                };

                var forecast = avg > 0
                    ? days >= avg
                        ? LF("DataPage_RerunForecastOver", (days - avg).ToString("0", CultureInfo.InvariantCulture))
                        : LF("DataPage_RerunForecastLeft", (avg - days).ToString("0", CultureInfo.InvariantCulture))
                    : string.Empty;

                cards.Add(new DcRerunCard
                {
                    Name = entry.Name!,
                    Avatar = entry.Avatar,
                    Star = entry.Star ?? 5,
                    DaysText = Fmt(entry.Days, 0),
                    DaysCaption = L("DataPage_RerunDays"),
                    AvgDaysText = LF("DataPage_RerunDaysValue", Fmt(entry.AvgDays, 0)),
                    UpTimesText = LF("DataPage_RerunUpTimes", entry.UpTimes ?? 0),
                    MaxGapText = LF("DataPage_RerunMaxGap", Fmt(entry.MaxGapDays, 0), entry.MaxGapPool ?? Dash),
                    MinGapText = LF("DataPage_RerunMinGap", Fmt(entry.MinGapDays, 0), entry.MinGapPool ?? Dash),
                    UrgencyText = urgencyText,
                    UrgencyTag = urgencyTag,
                    ForecastText = forecast,
                    ProgressValue = Math.Clamp(ratio * 100, 0, 100),
                    SortUrgency = ratio,
                    SortDays = days,
                    SortInterval = avg > 0 ? avg : double.MaxValue,
                    History = (entry.History ?? new List<string>()).Where(h => !string.IsNullOrWhiteSpace(h)).ToList()
                });
            }

            _rerunGroups.Add(cards);
        }

        ApplyRerunFilter();
    }

    private void BuildOverview()
    {
        OverviewKpis.Clear();
        OverviewInsights.Clear();
        OverviewRisers.Clear();
        OverviewFallers.Clear();
        OverviewTopTier.Clear();
        OverviewBanners.Clear();
        OverviewValuePicks.Clear();
        OverviewOverdue.Clear();

        var spiral = _spiralLatest?.Response;
        var stygian = _stygianLatest?.Response;

        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphInfo, Title = L("DataPage_KpiVersion"),
            Value = CleanVersion(spiral?.Version ?? _roleAvg?.Version),
            Caption = spiral?.UpdateInfo ?? string.Empty, ColorTag = "accent"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphChart, Title = L("DataPage_KpiSample"), Value = Compact(spiral?.SampleCount),
            Caption = L("DataPage_KpiStygianSample") + " " + Compact(stygian?.SampleCount), ColorTag = "accent"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphStarFill, Title = L("DataPage_KpiFullStar"), Value = spiral?.FullStarRate ?? Dash,
            Caption = L("DataPage_KpiOnceFullStar") + " " + (spiral?.FullStarOnceRate ?? Dash), ColorTag = "up"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphSync, Title = L("DataPage_KpiRestart"), Value = Fmt(spiral?.RestartTimesAvg, 1),
            Caption = L("DataPage_UnitTimesShort"), ColorTag = "accent"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphPeople, Title = L("DataPage_KpiCharacterCount"),
            Value = (_roleAvg?.Result?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
            Caption = _roleAvg?.LastUpdate ?? string.Empty, ColorTag = "accent"
        });
        OverviewKpis.Add(new DcKpiTile
        {
            Glyph = GlyphStar, Title = L("DataPage_KpiBannerCount"),
            Value = (_wish?.Characters?.Count ?? 0).ToString(CultureInfo.InvariantCulture),
            Caption = LF("DataPage_KpiWeaponBanner", _wish?.Weapons?.Count ?? 0), ColorTag = "accent"
        });

        foreach (var item in Spiral.Risers) OverviewRisers.Add(item);
        foreach (var item in Spiral.Fallers) OverviewFallers.Add(item);
        HasOverviewMovers = OverviewRisers.Count > 0 || OverviewFallers.Count > 0;
        OnPropertyChanged(nameof(HasOverviewMovers));
        
        var topNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in (_spiralLatest?.Tiers ?? new List<AbyssTierGroup>())
                 .Concat(_stygianLatest?.Tiers ?? new List<AbyssTierGroup>()))
        {
            if (NormalizeTierTag(group.RankClass) != "s1") continue;
            foreach (var member in group.List ?? new List<AbyssTierEntry>())
            {
                if (!string.IsNullOrEmpty(member.Name)) topNames.Add(member.Name!);
            }
        }

        var spiralRanks = IndexRanks(_spiralLatest);
        var position = 0;
        foreach (var card in _allCharacters.Where(c => topNames.Contains(c.Name))
                     .OrderByDescending(c => c.SortHeat).Take(12))
        {
            position++;
            spiralRanks.TryGetValue(card.Name, out var rank);
            var change = rank?.UseRateChange;
            var hasChange = change.HasValue && rank?.UseRateOld != null && Math.Abs(change.Value) > 0.05;

            OverviewTopTier.Add(new DcRankRow
            {
                Position = position,
                Name = card.Name,
                Avatar = card.Avatar,
                Star = card.Star,
                UseRate = card.SortAbyss,
                UseRateText = card.AbyssRateText,
                OwnRateText = card.OwnRateText,
                FieldShareText = PctText(card.SortAbyss * card.SortOwn / 100d),
                ConstellationText = card.ConstellationText,
                ClearTimeText = Dash,
                HasClearTime = false,
                HasChange = hasChange,
                ChangeText = hasChange ? SignedPct(change) : Dash,
                ChangeGlyph = hasChange ? change > 0 ? GlyphUp : GlyphDown : string.Empty,
                ChangeTag = !hasChange ? "flat" : change > 0 ? "up" : "down",
                TierText = card.TierText,
                TierTag = card.TierTag
            });
        }
        
        foreach (var banner in _allCharacterBanners.Where(b => b.StatusTag is "up" or "accent").Take(4))
        {
            OverviewBanners.Add(banner);
        }

        foreach (var banner in _allWeaponBanners.Where(b => b.StatusTag == "up").Take(2))
        {
            OverviewBanners.Add(banner);
        }

        HasOverviewBanners = OverviewBanners.Count > 0;
        OnPropertyChanged(nameof(HasOverviewBanners));
        
        var picks = _allCharacters
            .Where(c => c.Star == 5 && c.SortAbyss >= 30 && c.SortValue > 0)
            .OrderByDescending(c => c.SortValue)
            .Take(8)
            .ToList();

        var maxValue = picks.Select(c => c.SortValue).DefaultIfEmpty(1).Max();
        position = 0;
        foreach (var card in picks)
        {
            position++;
            OverviewValuePicks.Add(new DcCountRow
            {
                Position = position,
                Name = card.Name,
                Icon = card.Avatar,
                CountText = card.AbyssRateText,
                DetailText = L("DataPage_OwnRate") + " " + card.OwnRateText,
                Ratio = maxValue > 0 ? card.SortValue * 100d / maxValue : 0
            });
        }

        if (_rerunGroups.Count > 0)
        {
            foreach (var card in _rerunGroups[0].OrderByDescending(c => c.SortUrgency).Take(6))
            {
                OverviewOverdue.Add(card);
            }
        }

        BuildOverviewInsights();
    }

    private void BuildOverviewInsights()
    {
        var spiral = _spiralLatest?.Response;

        var leaders = OverviewTopTier.Take(3).Select(r => r.Name).ToList();
        if (spiral != null && leaders.Count > 0)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphChart, ColorTag = "accent", Title = L("DataPage_InsightMetaTitle"),
                Body = LF("DataPage_InsightMetaBody", CleanVersion(spiral.Version), spiral.FullStarRate ?? Dash,
                    Fmt(spiral.RestartTimesAvg, 1), string.Join(ListSep, leaders))
            });
        }

        if (OverviewRisers.FirstOrDefault() is { } riser)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphUp, ColorTag = "up", Title = L("DataPage_InsightRiseTitle"),
                Body = LF("DataPage_InsightRiseBody", riser.Name, riser.PreviousText, riser.CurrentText)
            });
        }

        if (OverviewFallers.FirstOrDefault() is { } faller)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphDown, ColorTag = "down", Title = L("DataPage_InsightFallTitle"),
                Body = LF("DataPage_InsightFallBody", faller.Name, faller.PreviousText, faller.CurrentText)
            });
        }

        if (OverviewValuePicks.FirstOrDefault() is { } pick &&
            _allCharacters.FirstOrDefault(c => c.Name == pick.Name) is { } pickCard)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphStarFill, ColorTag = "up", Title = L("DataPage_InsightValueTitle"),
                Body = LF("DataPage_InsightValueBody", pickCard.Name, pickCard.AbyssRateText, pickCard.OwnRateText)
            });
        }

        var ongoing = _allCharacterBanners.Where(b => b.StatusTag == "up").ToList();
        if (ongoing.Count > 0)
        {
            var names = ongoing.SelectMany(b => b.Star5).Select(s => s.Name).Distinct(StringComparer.Ordinal).ToList();
            var advice = new List<string>();

            foreach (var name in names.Take(4))
            {
                var card = _allCharacters.FirstOrDefault(c => c.Name == name);
                advice.Add(card == null
                    ? name
                    : LF("DataPage_BannerAdviceItem", name, card.TierText, card.AbyssRateText));
            }

            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphStar, ColorTag = "accent", Title = L("DataPage_InsightBannerTitle"),
                Body = LF("DataPage_InsightBannerBody", string.Join(ListSep, names), string.Join(ClauseSep, advice))
            });
        }
        else
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphStar, ColorTag = "flat", Title = L("DataPage_InsightBannerTitle"),
                Body = L("DataPage_InsightBannerNone")
            });
        }

        if (Spiral.AllTeams.FirstOrDefault() is { TeamNames.Length: > 0 } team)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphPeople, ColorTag = "accent", Title = L("DataPage_InsightTeamTitle"),
                Body = LF("DataPage_InsightTeamBody", team.TeamNames, team.UseRateText)
            });
        }

        if (OverviewOverdue.FirstOrDefault() is { } overdue)
        {
            OverviewInsights.Add(new DcInsight
            {
                Glyph = GlyphHistory, ColorTag = "up", Title = L("DataPage_InsightOverdueTitle"),
                Body = LF("DataPage_InsightOverdueBody", overdue.Name,
                    overdue.SortDays.ToString("0", CultureInfo.InvariantCulture),
                    overdue.SortInterval is > 0 and < double.MaxValue
                        ? overdue.SortInterval.ToString("0", CultureInfo.InvariantCulture)
                        : Dash)
            });
        }
    }

    private static Dictionary<string, AbyssRankEntry> IndexRanks(AbyssStatsBundle? bundle)
    {
        var map = new Dictionary<string, AbyssRankEntry>(StringComparer.Ordinal);
        foreach (var item in bundle?.Ranks ?? new List<AbyssRankEntry>())
        {
            if (!string.IsNullOrEmpty(item.Name)) map[item.Name!] = item;
        }

        return map;
    }

    private static Dictionary<string, AbyssTierEntry> IndexTiers(AbyssStatsBundle? bundle)
    {
        var map = new Dictionary<string, AbyssTierEntry>(StringComparer.Ordinal);
        foreach (var group in bundle?.Tiers ?? new List<AbyssTierGroup>())
        {
            foreach (var item in group.List ?? new List<AbyssTierEntry>())
            {
                if (!string.IsNullOrEmpty(item.Name)) map[item.Name!] = item;
            }
        }

        return map;
    }

    private Dictionary<string, RerunEntry> IndexRerun()
    {
        var map = new Dictionary<string, RerunEntry>(StringComparer.Ordinal);
        
        foreach (var group in (_rerun?.Result ?? new List<List<RerunEntry>>()).Take(2))
        {
            foreach (var entry in group)
            {
                if (!string.IsNullOrEmpty(entry.Name)) map[entry.Name!] = entry;
            }
        }

        return map;
    }
    
    private List<DcTeamCard> FindTeamsFor(string? avatar, string name, int take)
    {
        var source = Spiral.AllTeams.Count > 0 ? Spiral.AllTeams : Stygian.AllTeams;
        var result = new List<DcTeamCard>();

        foreach (var team in source)
        {
            var match = team.Members.Any(m =>
                (!string.IsNullOrEmpty(avatar) && string.Equals(m.Avatar, avatar, StringComparison.OrdinalIgnoreCase))
                || (!string.IsNullOrEmpty(name) && string.Equals(m.Name, name, StringComparison.Ordinal)));

            if (!match) continue;

            result.Add(team);
            if (result.Count >= take) break;
        }

        return result;
    }

    private static List<DcRateRow> BuildWeaponRows(List<WeaponUsageEntry>? source, int take)
    {
        var rows = new List<DcRateRow>();
        foreach (var item in (source ?? new List<WeaponUsageEntry>()).Take(take))
        {
            rows.Add(new DcRateRow
            {
                Name = item.Name ?? string.Empty,
                Icon = item.Avatar,
                Rate = item.Rate ?? 0,
                RateText = PctText(item.Rate),
                ColorTag = "accent"
            });
        }

        return rows;
    }

    private static List<DcRateRow> BuildArtifactRows(List<ArtifactUsageEntry>? source, int take)
    {
        var rows = new List<DcRateRow>();
        foreach (var item in (source ?? new List<ArtifactUsageEntry>()).Take(take))
        {
            rows.Add(new DcRateRow
            {
                Name = item.Name ?? string.Empty,
                Icon = item.Avatars is { Count: > 0 } avatars ? avatars[0] : null,
                Rate = item.Rate ?? 0,
                RateText = PctText(item.Rate),
                ColorTag = "accent"
            });
        }

        return rows;
    }

    private static (string text, string tag) ScoreToTier(double score) => score switch
    {
        >= 78 => ("T0", "s1"),
        >= 60 => ("T1", "s"),
        >= 42 => ("T2", "a"),
        >= 22 => ("T3", "b"),
        _ => ("T4", "f")
    };
    
    private static string NormalizeTierTag(string? rankClass) => rankClass?.ToLowerInvariant() switch
    {
        "s1" => "s1",
        "s" => "s",
        "a" => "a",
        "b" => "b",
        _ => "f"
    };

    private static string RankClassText(string? rankClass) => rankClass?.ToLowerInvariant() switch
    {
        "s1" => "S+",
        "s" => "S",
        "a" => "A",
        "b" => "B",
        "f" => "C",
        _ => Dash
    };

    private static string TierDescription(string? rankClass) => rankClass?.ToLowerInvariant() switch
    {
        "s1" => L("DataPage_TierS1Desc"),
        "s" => L("DataPage_TierSDesc"),
        "a" => L("DataPage_TierADesc"),
        "b" => L("DataPage_TierBDesc"),
        _ => L("DataPage_TierCDesc")
    };

    private static (DateTime? start, DateTime? end) ParseRange(string? range)
    {
        if (string.IsNullOrWhiteSpace(range)) return (null, null);
        
        var parts = range.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return (null, null);

        return (ParseDate(parts[0]), parts.Length > 1 ? ParseDate(parts[1]) : null);
    }

    private static DateTime? ParseDate(string text)
    {
        string[] formats = { "yyyy/MM/dd", "yyyy/M/d", "yyyy-MM-dd", "yyyy.MM.dd" };
        if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var exact))
        {
            return exact;
        }

        return DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose)
            ? loose
            : null;
    }
    
    private static string CleanVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return Dash;

        var index = version.IndexOfAny(new[] { ':', '：' });
        return index >= 0 && index < version.Length - 1 ? version[(index + 1)..].Trim() : version.Trim();
    }

    private static string Fmt(double? value, int decimals)
        => value.HasValue ? value.Value.ToString("F" + decimals, CultureInfo.InvariantCulture) : Dash;

    private static string PctText(double? value)
        => value.HasValue ? value.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : Dash;

    private static string SignedPct(double? value)
        => value.HasValue
            ? (value.Value > 0 ? "+" : string.Empty) + value.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : Dash;

    private static string NumText(double? value)
        => value.HasValue ? value.Value.ToString("N0", CultureInfo.CurrentCulture) : Dash;
    
    private static string Compact(double? value)
    {
        if (!value.HasValue) return Dash;
        var v = value.Value;

        return Math.Abs(v) switch
        {
            >= 1_000_000_000 => (v / 1_000_000_000).ToString("0.##", CultureInfo.InvariantCulture) + "B",
            >= 1_000_000 => (v / 1_000_000).ToString("0.##", CultureInfo.InvariantCulture) + "M",
            >= 10_000 => (v / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "K",
            _ => v.ToString("N0", CultureInfo.CurrentCulture)
        };
    }
    
    private static string ListSep => L("DataPage_ListSeparator");

    private static string ClauseSep => L("DataPage_ClauseSeparator");

    private static string L(string key) => key.GetLocalized();

    private static string LF(string key, params object?[] args)
    {
        var template = key.GetLocalized();
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
