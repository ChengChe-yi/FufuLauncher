using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Helpers;
using FufuLauncher.Models;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace FufuLauncher.ViewModels;

public partial class ServiceStatusViewModel : ObservableObject
{
    private static readonly SolidColorBrush HealthyBrush = new(Color.FromArgb(255, 46, 160, 67));
    private static readonly SolidColorBrush DegradedBrush = new(Color.FromArgb(255, 210, 153, 34));
    private static readonly SolidColorBrush OutageBrush = new(Color.FromArgb(255, 218, 54, 51));

    private readonly ServiceStatusService _service = new();
    private readonly HashSet<int> _seenMonitorIds = new();
    private CancellationTokenSource? _loadCts;
    private int _nextPage = 1;
    private int _totalMonitors;
    private int _perPage = 30;
    private int _totalPages;
    private bool _hasLoaded;

    public ObservableCollection<ServiceStatusMonitor> Monitors
    {
        get;
    } = new();

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _isLoadingMore;

    [ObservableProperty] private bool _hasError;

    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private string _overallTitle = string.Empty;

    [ObservableProperty] private string _overallCaption = string.Empty;

    [ObservableProperty] private string _uptimeText = "--";

    [ObservableProperty] private string _operationalCountText = string.Empty;

    [ObservableProperty] private string _outageCountText = string.Empty;

    [ObservableProperty] private string _pausedCountText = string.Empty;

    [ObservableProperty] private Brush _overallBrush = HealthyBrush;

    [ObservableProperty] private bool _hasMore;

    public bool IsBusy => IsLoading || IsLoadingMore;

    public bool CanRefresh => !IsBusy;

    public bool CanLoadMore => HasMore && !IsBusy;

    public bool ShowLoading => IsLoading && Monitors.Count == 0;

    public bool ShowContent => !ShowLoading;

    public bool ShowSummary => Monitors.Count > 0;

    public bool ShowFooter => HasMore;

    public bool ShowEmpty => _hasLoaded && !IsLoading && Monitors.Count == 0;

    public Visibility ShowLoadingVisibility => ShowLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShowContentVisibility => ShowContent ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShowSummaryVisibility => ShowSummary ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShowFooterVisibility => ShowFooter ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShowEmptyVisibility => ShowEmpty ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HasErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RefreshIconVisibility => IsLoading ? Visibility.Collapsed : Visibility.Visible;

    public Visibility RefreshProgressVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility LoadingMoreVisibility => IsLoadingMore ? Visibility.Visible : Visibility.Collapsed;

    public ServiceStatusViewModel()
    {
        Monitors.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ShowSummary));
            OnPropertyChanged(nameof(ShowLoading));
            OnPropertyChanged(nameof(ShowContent));
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(ShowSummaryVisibility));
            OnPropertyChanged(nameof(ShowLoadingVisibility));
            OnPropertyChanged(nameof(ShowContentVisibility));
            OnPropertyChanged(nameof(ShowEmptyVisibility));
        };
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var cts = BeginLoad();
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        _hasLoaded = false;
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowEmptyVisibility));

        try
        {
            var page = await _service.GetPageAsync(1, cts.Token);
            cts.Token.ThrowIfCancellationRequested();

            _seenMonitorIds.Clear();
            Monitors.Clear();
            _nextPage = 1;
            _totalMonitors = 0;
            _perPage = 30;
            _totalPages = 0;

            var added = MergePage(page);
            _nextPage = 2;
            ApplyPage(page, added);
            _hasLoaded = true;
            OnPropertyChanged(nameof(ShowEmpty));
            OnPropertyChanged(nameof(ShowEmptyVisibility));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (!cts.IsCancellationRequested)
            {
                HasError = true;
                ErrorMessage = "ServiceStatus_ErrorHint".GetLocalized();
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                IsLoading = false;
                _loadCts = null;
            }

            cts.Dispose();
        }
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!HasMore || IsBusy)
        {
            return;
        }

        var cts = BeginLoad();
        var requestedPage = _nextPage;
        IsLoadingMore = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var page = await _service.GetPageAsync(requestedPage, cts.Token);
            cts.Token.ThrowIfCancellationRequested();

            var added = MergePage(page);
            _nextPage = requestedPage + 1;
            ApplyPage(page, added);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (!cts.IsCancellationRequested)
            {
                HasError = true;
                ErrorMessage = "ServiceStatus_ErrorHint".GetLocalized();
            }
        }
        finally
        {
            if (ReferenceEquals(_loadCts, cts))
            {
                IsLoadingMore = false;
                _loadCts = null;
            }

            cts.Dispose();
        }
    }

    public void Cancel()
    {
        _loadCts?.Cancel();
    }

    private CancellationTokenSource BeginLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        return _loadCts;
    }

    private int MergePage(ServiceStatusPage page)
    {
        var added = 0;
        foreach (var monitor in page.Monitors)
        {
            if (!_seenMonitorIds.Add(monitor.MonitorId))
            {
                continue;
            }

            Monitors.Add(monitor);
            added++;
        }

        return added;
    }

    private void ApplyPage(ServiceStatusPage page, int added)
    {
        if (page.TotalMonitors > 0)
        {
            _totalMonitors = page.TotalMonitors;
        }

        if (page.PerPage > 0)
        {
            _perPage = page.PerPage;
        }

        _totalPages = _totalMonitors > 0 ? (int)Math.Ceiling((double)_totalMonitors / _perPage) : 0;
        HasMore = added > 0 && HasMorePages(page);
        UpdateSummary(page);
    }

    private bool HasMorePages(ServiceStatusPage page)
    {
        if (page.Monitors.Count == 0)
        {
            return false;
        }

        if (_totalPages > 0 && _nextPage > _totalPages)
        {
            return false;
        }

        if (_totalMonitors > 0)
        {
            return Monitors.Count < _totalMonitors;
        }

        return page.PerPage > 0 && page.Monitors.Count >= page.PerPage;
    }

    private void UpdateSummary(ServiceStatusPage page)
    {
        var up = page.UpCount;
        var down = page.DownCount;
        var paused = page.PausedCount;

        if (up + down + paused == 0 && Monitors.Count > 0)
        {
            up = Monitors.Count(m => m.StatusClass == "success");
            down = Monitors.Count(m => m.StatusClass == "danger");
            paused = Monitors.Count(m => m.StatusClass == "paused");
        }

        var total = Math.Max(_totalMonitors, Monitors.Count);
        OperationalCountText = string.Format("ServiceStatus_CountOperational".GetLocalized(), up);
        OutageCountText = string.Format("ServiceStatus_CountOutage".GetLocalized(), down);
        PausedCountText = string.Format("ServiceStatus_CountPaused".GetLocalized(), paused);

        var averageUptime = Monitors.Count > 0 ? Monitors.Average(m => m.Uptime90) : 0;
        UptimeText = Monitors.Count > 0 ? $"{averageUptime:0.00}%" : "--";

        var hasOutage = down > 0 || Monitors.Any(m => m.StatusClass == "danger");
        var hasDegraded = Monitors.Any(m => m.StatusClass == "warning");

        OverallTitle = hasOutage || hasDegraded
            ? "ServiceStatus_SomeIssues".GetLocalized()
            : "ServiceStatus_AllOperational".GetLocalized();
        OverallCaption =
            $"{string.Format("ServiceStatus_CountFormat".GetLocalized(), total)} · {string.Format("ServiceStatus_Updated".GetLocalized(), DateTime.Now.ToString("HH:mm"))}";

        OverallBrush = hasOutage
            ? OutageBrush
            : hasDegraded || (up == 0 && paused > 0)
                ? DegradedBrush
                : HealthyBrush;
    }

    partial void OnIsLoadingChanged(bool value)
    {
        RaiseStateChanged();
    }

    partial void OnIsLoadingMoreChanged(bool value)
    {
        RaiseStateChanged();
    }

    partial void OnHasMoreChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowFooter));
        OnPropertyChanged(nameof(ShowFooterVisibility));
        OnPropertyChanged(nameof(CanLoadMore));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(HasErrorVisibility));
    }

    private void RaiseStateChanged()
    {
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(CanLoadMore));
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(ShowContent));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowLoadingVisibility));
        OnPropertyChanged(nameof(ShowContentVisibility));
        OnPropertyChanged(nameof(ShowEmptyVisibility));
        OnPropertyChanged(nameof(RefreshIconVisibility));
        OnPropertyChanged(nameof(RefreshProgressVisibility));
        OnPropertyChanged(nameof(LoadingMoreVisibility));
    }
}