/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Models.CycleChallenge;
using FufuLauncher.Services;
using FufuLauncher.Services.CycleChallenge;

namespace FufuLauncher.ViewModels;

public sealed class CycleChallengePeriodOption
{
    public CycleChallengePeriodOption(ChallengePeriod period, TimeSpan displayOffset)
    {
        Period = period;
        Display = BuildDisplay(period, displayOffset);
    }

    public ChallengePeriod Period
    {
        get;
    }

    public string Display
    {
        get;
    }

    public string ScheduleId => Period.ScheduleId;


    private static string BuildDisplay(ChallengePeriod period, TimeSpan displayOffset)
    {
        var start = CycleChallengeTime.FormatStoredString(period.StartUtc, "yyyy-MM-dd HH:mm", displayOffset);
        var end = CycleChallengeTime.FormatStoredString(period.EndUtc, "yyyy-MM-dd HH:mm", displayOffset);
        var range = string.IsNullOrEmpty(start) || string.IsNullOrEmpty(end)
            ? string.Empty
            : $"{start} ~ {end}";
        return string.IsNullOrEmpty(range) ? $"{period.ScheduleId}" : $"{period.ScheduleId}  {range}";
    }
}

public sealed partial class CycleChallengeViewModel : ObservableObject
{
    private readonly AccountManager _accountManager;
    private readonly GameRoleService _gameRoleService;
    private readonly CycleChallengeService _service;

    private CancellationTokenSource? _cts;
    private int _loadVersion;
    private bool _suppressPeriodReload;
    private DateTimeOffset? _lastFetchedAt;


    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(30);

    public CycleChallengeViewModel(
        AccountManager accountManager,
        GameRoleService gameRoleService,
        CycleChallengeService service)
    {
        _accountManager = accountManager;
        _gameRoleService = gameRoleService;
        _service = service;

        WeakReferenceMessenger.Default.Register<CycleChallengeViewModel, AccountChangedMessage>(
            this, static (recipient, _) => recipient.OnContextChanged());
        WeakReferenceMessenger.Default.Register<CycleChallengeViewModel, GameRoleChangedMessage>(
            this, static (recipient, _) => recipient.OnContextChanged());
        WeakReferenceMessenger.Default.Register<CycleChallengeViewModel, FeatureGameRoleChangedMessage>(
            this, static (recipient, message) =>
            {
                if (ReferenceEquals(message.Scope, recipient.RoleSelection))
                {
                    recipient.OnContextChanged();
                }
            });
    }


    public GameRoleScope RoleSelection
    {
        get;
    } = new();

    public ObservableCollection<CycleChallengePeriodOption> Periods
    {
        get;
    } = [];

    [ObservableProperty] private ChallengeMode _currentMode = ChallengeMode.SpiralAbyss;

    [ObservableProperty] private CycleChallengePeriodOption? _selectedPeriod;

    [ObservableProperty] private string? _payload;

    [ObservableProperty] private string _accountText = string.Empty;

    [ObservableProperty] private bool _isLoading;

    [ObservableProperty] private bool _hasError;

    [ObservableProperty] private string? _errorMessage;

    [ObservableProperty] private bool _isOffline;

    [ObservableProperty] private string? _statusText;

    [ObservableProperty] private bool _hasData;


    public string? CurrentRegion
    {
        get;
        private set;
    }

    partial void OnSelectedPeriodChanged(CycleChallengePeriodOption? value)
    {
        if (value is null || _suppressPeriodReload)
        {
            return;
        }

        _ = LoadPeriodAsync(value.ScheduleId);
    }

    public async Task InitializeAsync()
    {
        RefreshHeader();


        _ = Task.Run(async () =>
        {
            try
            {
                await RemoteImageCache.CleanupAsync().ConfigureAwait(false);
            }
            catch
            {
            }
        });

        await ReloadAsync(forceRefresh: false);


        if (ShouldRefresh())
        {
            await ReloadAsync(forceRefresh: true);
        }
    }

    private bool ShouldRefresh() =>
        !HasData || (_lastFetchedAt is { } fetched && DateTimeOffset.UtcNow - fetched > CacheTtl);

    [RelayCommand]
    private async Task RefreshAsync() => await ReloadAsync(forceRefresh: true);

    public async Task SetModeAsync(ChallengeMode mode)
    {
        if (CurrentMode == mode)
        {
            return;
        }

        CurrentMode = mode;
        await ReloadAsync(forceRefresh: false);


        if (ShouldRefresh())
        {
            await ReloadAsync(forceRefresh: true);
        }
    }


    public void OnContextChanged()
    {
        _cts?.Cancel();
        RoleSelection.Reset();
        RefreshHeader();
        _ = ReloadAsync(forceRefresh: false);
    }

    private async Task ReloadAsync(bool forceRefresh)
    {
        _cts?.Cancel();
        var cts = new CancellationTokenSource();
        _cts = cts;
        var version = ++_loadVersion;

        IsLoading = true;
        HasError = false;
        ErrorMessage = null;
        IsOffline = false;
        StatusText = null;

        try
        {
            FufuLauncher.Services.SelectedGameRole? role;
            try
            {
                role = await _gameRoleService.GetCurrentAsync(RoleSelection).ConfigureAwait(true);
                if (role is null)
                {
                    SetNoAccountState();
                    return;
                }

                CurrentRegion = role.Role.region;
            }
            catch (Exception ex)
            {
                SetError(ChallengeErrorKind.LoginExpired, ex.Message);
                return;
            }

            var result = forceRefresh
                ? await _service.RefreshAsync(CurrentMode, role, cts.Token).ConfigureAwait(true)
                : _service.LoadFromCache(CurrentMode, role, null);

            if (version != _loadVersion)
            {
                return;
            }

            ApplyResult(result);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                SetError(ChallengeErrorKind.NetworkError, ex.Message);
            }
        }
        finally
        {
            if (version == _loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    private async Task LoadPeriodAsync(string scheduleId)
    {
        try
        {
            var role = await _gameRoleService.GetCurrentAsync(RoleSelection).ConfigureAwait(true);
            if (role is null)
            {
                return;
            }

            CurrentRegion = role.Role.region;

            var result = _service.LoadFromCache(CurrentMode, role, scheduleId);
            if (result.Success)
            {
                Payload = result.Payload;
                HasData = !string.IsNullOrEmpty(result.Payload);
                IsOffline = false;
                HasError = false;
                _lastFetchedAt = result.FetchedAt;
                StatusText = "CycleChallenge_DataStatusCached".GetLocalized();
            }
        }
        catch
        {
        }
    }

    private void ApplyResult(ChallengeLoadResult result)
    {
        HasData = result.Success && !string.IsNullOrEmpty(result.Payload);
        Payload = result.Payload;

        Periods.Clear();
        var displayOffset = CycleChallengeTime.GetDisplayOffset(CurrentRegion);
        foreach (var period in result.Periods)
        {
            Periods.Add(new CycleChallengePeriodOption(period, displayOffset));
        }

        var active = Periods.FirstOrDefault(x => x.ScheduleId == result.ActiveScheduleId) ?? Periods.FirstOrDefault();
        if (active is not null && !ReferenceEquals(active, SelectedPeriod))
        {
            _suppressPeriodReload = true;
            try
            {
                SelectedPeriod = active;
            }
            finally
            {
                _suppressPeriodReload = false;
            }
        }

        if (!result.Success)
        {
            SetError(result.Kind, result.Message);
            return;
        }

        _lastFetchedAt = result.FetchedAt;

        IsOffline = result.IsFromCache && result.Kind != ChallengeErrorKind.Success;
        HasError = IsOffline;
        ErrorMessage = HasError ? result.Message : null;
        StatusText = result.IsFromCache
            ? "CycleChallenge_DataStatusCached".GetLocalized()
            : "CycleChallenge_DataStatusFresh".GetLocalized();

        if (!HasData)
        {
            StatusText = "CycleChallenge_NotParticipated".GetLocalized();
        }
    }

    private void SetError(ChallengeErrorKind kind, string? message)
    {
        HasData = false;
        Payload = null;
        HasError = true;
        ErrorMessage = kind switch
        {
            ChallengeErrorKind.LoginExpired => "CycleChallenge_LoginExpired".GetLocalized(),
            ChallengeErrorKind.RiskControlRequired => "CycleChallenge_RiskControl".GetLocalized(),
            ChallengeErrorKind.RateLimited => "CycleChallenge_RateLimited".GetLocalized(),
            ChallengeErrorKind.Unsupported => "CycleChallenge_Unsupported".GetLocalized(),
            ChallengeErrorKind.NotParticipated => "CycleChallenge_NotParticipated".GetLocalized(),
            _ => message ?? "CycleChallenge_LoadFailed".GetLocalized(),
        };
    }

    private void SetNoAccountState()
    {
        HasData = false;
        Payload = null;
        HasError = true;
        ErrorMessage = "CycleChallenge_NoAccount".GetLocalized();
        StatusText = null;
    }

    private void RefreshHeader()
    {
        var account = _accountManager.GetActiveAccountEntry();
        if (account is null)
        {
            AccountText = "CycleChallenge_NoAccount".GetLocalized();
            return;
        }

        var role = GameRoleSelection.Current(account);
        AccountText = role is null
            ? $"{account.Nickname}"
            : $"{account.Nickname}  ·  {role.game_uid}  {role.region_name}";
    }

    private readonly record struct SelectedRoleResult(FufuLauncher.Services.SelectedGameRole Role);
}