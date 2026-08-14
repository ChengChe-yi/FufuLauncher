/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Models.DataCenter;

namespace FufuLauncher.ViewModels;

public sealed partial class DataViewModel
{
    #region Loading & Refresh

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await LoadAllAsync(false);
    }

    public Task RefreshAsync() => LoadAllAsync(true);

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

    #endregion
}
