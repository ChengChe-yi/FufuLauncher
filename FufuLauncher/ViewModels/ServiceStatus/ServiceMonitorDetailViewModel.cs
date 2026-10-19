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

public partial class ServiceMonitorDetailViewModel : ObservableObject
{
    private const int EventPageSize = 8;

    private static readonly SolidColorBrush HealthyBrush = new(Color.FromArgb(255, 46, 160, 67));

    private readonly ServiceStatusService _service = new();
    private readonly List<ServiceMonitorEvent> _events = new();
    private CancellationTokenSource? _loadCts;
    private int _shownEvents;

    public int MonitorId
    {
        get;
    }

    public string MonitorName
    {
        get;
    }

    public ObservableCollection<ServiceMonitorEvent> VisibleEvents
    {
        get;
    } = new();

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _hasError;

    [ObservableProperty] private string _errorMessage = string.Empty;

    [ObservableProperty] private ServiceMonitorDetail? _detail;

    [ObservableProperty] private string _statusText = string.Empty;

    [ObservableProperty] private Brush _statusBrush = HealthyBrush;

    [ObservableProperty] private string _headerCaption = string.Empty;

    [ObservableProperty] private string _uptime1Text = "--";

    [ObservableProperty] private string _uptime7Text = "--";

    [ObservableProperty] private string _uptime30Text = "--";

    [ObservableProperty] private string _uptime90Text = "--";

    [ObservableProperty] private string _averageResponseText = "--";

    [ObservableProperty] private string _maxResponseText = "--";

    [ObservableProperty] private string _minResponseText = "--";

    [ObservableProperty] private string _totalChecksText = "--";

    [ObservableProperty] private bool _hasEvents;

    [ObservableProperty] private bool _canShowMoreEvents;

    public bool ShowLoading => IsLoading && Detail == null;

    public bool CanRefresh => !IsLoading;

    public Visibility LoadingVisibility => ShowLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ContentVisibility => ShowLoading ? Visibility.Collapsed : Visibility.Visible;

    public Visibility DetailVisibility => Detail != null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ErrorVisibility => HasError ? Visibility.Visible : Visibility.Collapsed;

    public Visibility RefreshIconVisibility => IsLoading ? Visibility.Collapsed : Visibility.Visible;

    public Visibility RefreshProgressVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;

    public Visibility EventsVisibility => HasEvents ? Visibility.Visible : Visibility.Collapsed;

    public Visibility NoEventsVisibility =>
        Detail != null && !HasEvents ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ShowMoreVisibility =>
        CanShowMoreEvents ? Visibility.Visible : Visibility.Collapsed;

    public ServiceMonitorDetailViewModel(int monitorId, string monitorName)
    {
        MonitorId = monitorId;
        MonitorName = monitorName;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsLoading)
        {
            return;
        }

        var cts = BeginLoad();
        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;

        try
        {
            var detail = await _service.GetMonitorDetailAsync(MonitorId, cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            ApplyDetail(detail);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
            if (!cts.IsCancellationRequested)
            {
                HasError = true;
                ErrorMessage = "ServiceDetail_ErrorHint".GetLocalized();
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
    private void ShowMoreEvents()
    {
        if (!CanShowMoreEvents)
        {
            return;
        }

        AppendEvents(EventPageSize);
    }

    public void Cancel()
    {
        _loadCts?.Cancel();
    }

    private void ApplyDetail(ServiceMonitorDetail detail)
    {
        Detail = detail;
        StatusText = detail.StatusText;
        StatusBrush = detail.StatusBrush;
        HeaderCaption = detail.HeaderCaption;
        Uptime1Text = detail.Uptime1Text;
        Uptime7Text = detail.Uptime7Text;
        Uptime30Text = detail.Uptime30Text;
        Uptime90Text = detail.Uptime90Text;
        AverageResponseText = detail.AverageResponseText;
        MaxResponseText = detail.MaxResponseText;
        MinResponseText = detail.MinResponseText;
        TotalChecksText = detail.TotalChecksText;

        _events.Clear();
        _events.AddRange(detail.Events);
        VisibleEvents.Clear();
        _shownEvents = 0;
        HasEvents = _events.Count > 0;
        AppendEvents(EventPageSize);

        OnPropertyChanged(nameof(DetailVisibility));
        OnPropertyChanged(nameof(NoEventsVisibility));
        OnPropertyChanged(nameof(ContentVisibility));
        OnPropertyChanged(nameof(LoadingVisibility));
    }

    private void AppendEvents(int count)
    {
        var remaining = _events.Count - _shownEvents;
        if (remaining <= 0)
        {
            CanShowMoreEvents = false;
            return;
        }

        var take = Math.Min(count, remaining);
        for (var i = 0; i < take; i++)
        {
            VisibleEvents.Add(_events[_shownEvents + i]);
        }

        _shownEvents += take;
        CanShowMoreEvents = _shownEvents < _events.Count;
    }

    private CancellationTokenSource BeginLoad()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        return _loadCts;
    }

    partial void OnIsLoadingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLoading));
        OnPropertyChanged(nameof(CanRefresh));
        OnPropertyChanged(nameof(LoadingVisibility));
        OnPropertyChanged(nameof(ContentVisibility));
        OnPropertyChanged(nameof(RefreshIconVisibility));
        OnPropertyChanged(nameof(RefreshProgressVisibility));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(ErrorVisibility));
    }

    partial void OnHasEventsChanged(bool value)
    {
        OnPropertyChanged(nameof(EventsVisibility));
        OnPropertyChanged(nameof(NoEventsVisibility));
    }

    partial void OnCanShowMoreEventsChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowMoreVisibility));
    }
}