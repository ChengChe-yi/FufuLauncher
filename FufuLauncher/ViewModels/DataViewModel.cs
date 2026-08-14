/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.ComponentModel;
using System.Runtime.CompilerServices;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Models.DataCenter;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels;

public sealed partial class DataViewModel : INotifyPropertyChanged
{
    #region Core & State

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
    private readonly INotificationService _notificationService;

    private RoleAvgResponse? _roleAvg;

    private AbyssStatsBundle? _spiralLatest;
    private AbyssStatsBundle? _stygianLatest;

    private AbyssStatsBundle? _spiralView;
    private AbyssStatsBundle? _stygianView;

    private WishHistoryResponse? _wish;
    private RerunResponse? _rerun;

    private Dictionary<string, WishCharacterStat> _wishStats = new(StringComparer.Ordinal);

    private readonly List<DcCharacterCard> _allCharacters = new();
    private readonly List<DcWishBanner> _allCharacterBanners = new();
    private readonly List<DcWishBanner> _allWeaponBanners = new();
    private readonly List<List<DcRerunCard>> _rerunGroups = new();

    private CancellationTokenSource? _cts;
    private bool _isInitialized;

    public DataViewModel(GameStatsService stats, IDataCenterPdfReportService pdfReport,
        INotificationService notificationService)
    {
        _stats = stats;
        _pdfReport = pdfReport;
        _notificationService = notificationService;

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
            OnPropertyChanged(nameof(ShowInitialSkeleton));
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

    public bool ShowInitialSkeleton => IsLoading && !HasExportableData && !HasError;

    private bool _hasError;

    public bool HasError
    {
        get => _hasError;
        private set
        {
            if (_hasError == value) return;
            _hasError = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowInitialSkeleton));
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

    #endregion

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
