/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Messages;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Models;
using FufuLauncher.Models.Genshin;
using FufuLauncher.Services;
using MihoyoBBS;

namespace FufuLauncher.ViewModels;

public class GenshinViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IGenshinService _genshinService;
    private readonly GameRoleService _roles;
    public GameRoleScope RoleSelection { get; } = new();
    private int _loadVersion;
    private bool _disposed;

    private string _uid = string.Empty;
    public string Uid
    {
        get => _uid;
        set
        {
            _uid = value; OnPropertyChanged();
        }
    }

    private string _nickname = string.Empty;
    public string Nickname
    {
        get => _nickname;
        set
        {
            _nickname = value; OnPropertyChanged();
        }
    }

    private TravelersDiarySummary? _travelersDiary;
    public TravelersDiarySummary? TravelersDiary
    {
        get => _travelersDiary;
        set
        {
            _travelersDiary = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(FormattedDate));
            OnPropertyChanged(nameof(FormattedMonthPrimogems));
            OnPropertyChanged(nameof(FormattedMonthMora));
            OnPropertyChanged(nameof(IncomeSources));
        }
    }

    public string FormattedDate => _travelersDiary?.Data?.Date ?? "--";
    public string FormattedMonthPrimogems => _travelersDiary?.Data?.MonthData?.CurrentPrimogems.ToString("N0") ?? "0";
    public string FormattedMonthMora => _travelersDiary?.Data?.MonthData?.CurrentMora.ToString("N0") ?? "0";

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value; OnPropertyChanged();
        }
    }

    private string _statusMessage = "等待加载数据...";
    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            _statusMessage = value; OnPropertyChanged();
        }
    }

    public List<IncomeSourceViewModel> IncomeSources
    {
        get
        {
            if (TravelersDiary?.Data?.MonthData?.GroupBy == null)
                return new List<IncomeSourceViewModel>();

            return TravelersDiary.Data.MonthData.GroupBy
                .Where(s => s.Num > 0)
                .OrderByDescending(s => s.Num)
                .Select(s => new IncomeSourceViewModel
                {
                    Action = s.Action,
                    Num = s.Num,
                    Percent = s.Percent,
                    Color = GetIncomeSourceColor(s.ActionId)
                })
                .ToList();
        }
    }

    public IAsyncRelayCommand LoadDataCommand
    {
        get;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    
    public GenshinViewModel(
        IGenshinService genshinService,
        GameRoleService roles)
    {
        _genshinService = genshinService;
        _roles = roles;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        WeakReferenceMessenger.Default.Register<GameRoleChangedMessage>(this, (r, m) =>
        {
            if (!RoleSelection.HasOverride) dispatcher.TryEnqueue(async () => await LoadDataAsync());
        });
        WeakReferenceMessenger.Default.Register<FeatureGameRoleChangedMessage>(this, (r, m) =>
        {
            if (ReferenceEquals(m.Scope, RoleSelection)) dispatcher.TryEnqueue(async () => await LoadDataAsync());
        });
        WeakReferenceMessenger.Default.Register<GameRolesUpdatedMessage>(this, (r, m) =>
        {
            var account = App.GetService<AccountManager>().GetActiveAccountEntry();
            if (account?.Id == m.AccountId && RoleSelection.Current(account) == null)
                dispatcher.TryEnqueue(async () => await LoadDataAsync());
        });
        WeakReferenceMessenger.Default.Register<AccountChangedMessage>(this, (r, m) =>
        {
            RoleSelection.Reset();
            dispatcher.TryEnqueue(async () => await LoadDataAsync());
        });
    }

    public void Dispose()
    {
        _disposed = true;
        ++_loadVersion;
        WeakReferenceMessenger.Default.UnregisterAll(this);
    }

    private async Task LoadDataAsync()
    {
        if (_disposed) return;
        var version = ++_loadVersion;
        TravelersDiary = null;
        Uid = Nickname = "";

        try
        {
            IsLoading = true;
            StatusMessage = "正在连接米游社...";

     
            var selected = await _roles.GetCurrentAsync(RoleSelection);
            if (version != _loadVersion || _disposed) return;
            if (selected == null)
            {
                StatusMessage = "需先登录账号";
                return;
            }

            string cookie = string.Join("; ", selected.Cookies.Select(kv => $"{kv.Key}={kv.Value}"));
            var role = selected.Role;

            Uid = role.game_uid;
            Nickname = role.nickname;

            StatusMessage = "分析旅行札记...";
            var diary = await _genshinService.GetTravelersDiarySummaryAsync(
                Uid, cookie, role.region, DateTime.Now.Month);
            if (version != _loadVersion || _disposed || !_roles.IsCurrent(selected, RoleSelection)) return;
            if (diary.Retcode != 0)
                throw new InvalidOperationException($"旅行札记请求失败（{diary.Retcode}）：{diary.Message}");
            if (diary.Data.Uid.ToString() != role.game_uid || diary.Data.Region != role.region)
                throw new InvalidOperationException("旅行札记返回的角色与当前选择不一致，已停止显示。");
            TravelersDiary = diary;

            StatusMessage = "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error: {ex.Message}");
            if (version == _loadVersion && !_disposed) StatusMessage = $"加载失败: {ex.Message}";
        }
        finally
        {
            if (version == _loadVersion && !_disposed) IsLoading = false;
        }
    }

    private string GetIncomeSourceColor(int actionId)
    {
        return actionId switch
        {
            1 => "#FF7675",
            2 => "#FAB1A0",
            3 => "#74B9FF",
            4 => "#55EFC4",
            5 => "#81ECEC",
            6 => "#FFEAA7",
            11 => "#A29BFE",
            _ => "#B2BEC3"
        };
    }
}

public class IncomeSourceViewModel
{
    public string Action { get; set; } = "";
    public int Num
    {
        get; set;
    }
    public int Percent
    {
        get; set;
    }
    public string FormattedPercent => $"{Percent}%";
    public string Color { get; set; } = "#000000";
}
