/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Models;
using FufuLauncher.Services;
using MihoyoBBS;

namespace FufuLauncher.ViewModels;

public partial class AccountViewModel : ObservableRecipient
{
    #region 字段

    private readonly ILocalSettingsService _localSettingsService;
    private readonly IUserInfoService _userInfoService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
    private const int MaxAccounts = 4;
    private readonly AccountManager _accountManager;
    private int _loadVersion;
    private string? _lastNotifiedAccountId;
    private volatile bool _isDisposed;

    #endregion

    #region 生命周期

    public void Cleanup()
    {
        _isDisposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        Interlocked.Increment(ref _loadVersion);
    }

    #endregion

    #region 属性

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
    [NotifyPropertyChangedFor(nameof(IsNotLoggedIn))]
    private AccountInfo? _currentAccount;

    public bool IsLoggedIn => CurrentAccount != null;
    public bool IsNotLoggedIn => CurrentAccount == null;

    public IRelayCommand OpenSecurityCenterCommand
    {
        get;
    }

    [ObservableProperty] private string _loginButtonText = "Account_LoginMiyoushe".GetLocalized();
    [ObservableProperty] private string _statusMessage = "";

    [ObservableProperty] private GameRolesResponse? _gameRolesInfo;
    [ObservableProperty] private UserFullInfoResponse? _userFullInfo;
    [ObservableProperty] private bool _isLoadingUserInfo;

    [ObservableProperty] private ObservableCollection<AccountInfo> _savedAccounts = new();
    public bool HasSavedAccounts => SavedAccounts.Count > 0;

    public bool HasCommunityData => UserFullInfo?.data?.user_info?.achieve != null;
    public string CommunityLikeCount => UserFullInfo?.data?.user_info?.achieve?["like_num"]?.ToString() ?? "-";
    public string CommunityPostCount => UserFullInfo?.data?.user_info?.achieve?["post_num"]?.ToString() ?? "-";
    public string CommunityReplyCount => UserFullInfo?.data?.user_info?.achieve?["replypost_num"]?.ToString() ?? "-";


    public List<GameRoleInfo>? BoundRoles => GameRolesInfo?.data?.list;

    #endregion

    #region 命令

    public IRelayCommand LockAccountCommand
    {
        get;
    }

    public IRelayCommand LoginCommand
    {
        get;
    }

    public IRelayCommand LogoutCommand
    {
        get;
    }

    public IRelayCommand LoadUserInfoCommand
    {
        get;
    }

    public IRelayCommand OpenGenshinDataCommand
    {
        get;
    }

    public IRelayCommand CopyCookieCommand
    {
        get;
    }

    public IRelayCommand RefreshCookieCommand
    {
        get;
    }

    public IRelayCommand AddAccountCommand
    {
        get;
    }

    public IRelayCommand<AccountInfo> SwitchAccountCommand
    {
        get;
    }

    #endregion

    #region 构造函数

    public AccountViewModel(
        ILocalSettingsService localSettingsService,
        IUserInfoService userInfoService,
        INavigationService navigationService,
        INotificationService notificationService,
        AccountManager accountManager)
    {
        _localSettingsService = localSettingsService;
        _userInfoService = userInfoService;
        _navigationService = navigationService;
        _notificationService = notificationService;
        _dispatcherQueue = App.MainWindow.DispatcherQueue;
        _accountManager = accountManager;
        WeakReferenceMessenger.Default.Register<GameRoleChangedMessage>(this, (r, m) =>
            RunOnUIThread(() =>
            {
                if (_isDisposed || CurrentAccount?.AccountId != m.AccountId ||
                    _accountManager.ActiveAccountId != m.AccountId) return;
                var role = GameRoleSelection.Current(_accountManager.GetActiveAccountEntry()!);
                CurrentAccount.GameUid = role?.game_uid ?? "";
                CurrentAccount.Level = role?.level.ToString() ?? "";
                CurrentAccount.Server = role?.region_name ?? "";
            }));
        LoginCommand = new AsyncRelayCommand(LoginAsync);
        LogoutCommand = new AsyncRelayCommand(LogoutAsync);
        LoadUserInfoCommand = new AsyncRelayCommand(async () => await LoadUserInfoAsync());
        OpenGenshinDataCommand = new AsyncRelayCommand(OpenGenshinDataAsync);
        CopyCookieCommand = new AsyncRelayCommand(CopyCookieAsync);
        RefreshCookieCommand = new AsyncRelayCommand(RefreshCookieAsync);
        AddAccountCommand = new AsyncRelayCommand(AddNewAccountAsync);
        SwitchAccountCommand = new AsyncRelayCommand<AccountInfo>(SwitchToAccountAsync);
        OpenSecurityCenterCommand = new AsyncRelayCommand(OpenSecurityCenterAsync);
        LockAccountCommand = new AsyncRelayCommand(LockAccountAsync);
    }

    #endregion

    private void RunOnUIThread(Action action)
    {
        if (_isDisposed || _dispatcherQueue == null) return;
        if (_dispatcherQueue.HasThreadAccess)
        {
            if (!_isDisposed) action();
        }
        else
            _dispatcherQueue.TryEnqueue(() =>
            {
                if (!_isDisposed) action();
            });
    }
}