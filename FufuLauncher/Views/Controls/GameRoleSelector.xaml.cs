/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.Messaging;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Helpers;
using FufuLauncher.Messages;
using FufuLauncher.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class GameRoleSelector : UserControl
{
    public static readonly DependencyProperty ScopeProperty = DependencyProperty.Register(
        nameof(Scope), typeof(GameRoleScope), typeof(GameRoleSelector),
        new PropertyMetadata(null, (sender, _) =>
        {
            if (sender is GameRoleSelector selector && selector.IsLoaded) selector.UpdateItems();
        }));
    public GameRoleScope? Scope
    {
        get => (GameRoleScope?)GetValue(ScopeProperty);
        set => SetValue(ScopeProperty, value);
    }
    private readonly AccountManager _accounts = App.GetService<AccountManager>();
    private readonly GameRoleService _roles = App.GetService<GameRoleService>();
    private List<GameRoleInfo> _items = new();
    private string? _accountId;
    private bool _updating;
    private int _loadVersion;

    public GameRoleSelector()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += (_, _) =>
        {
            ++_loadVersion;
            WeakReferenceMessenger.Default.UnregisterAll(this);
        };
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Register<AccountChangedMessage>(this, (r, m) =>
            DispatcherQueue.TryEnqueue(async () => await LoadAsync(true)));
        WeakReferenceMessenger.Default.Register<GameRoleChangedMessage>(this, (r, m) =>
            DispatcherQueue.TryEnqueue(UpdateItems));
        WeakReferenceMessenger.Default.Register<FeatureGameRoleChangedMessage>(this, (r, m) =>
        {
            if (ReferenceEquals(m.Scope, Scope)) DispatcherQueue.TryEnqueue(UpdateItems);
        });
        WeakReferenceMessenger.Default.Register<GameRolesUpdatedMessage>(this, (r, m) =>
            DispatcherQueue.TryEnqueue(UpdateItems));
        await LoadAsync(true);
    }

    private void UpdateItems()
    {
        _updating = true;
        try
        {
            var account = _accounts.GetActiveAccountEntry();
            _accountId = account?.Id;
            _items = account == null ? new() : GameRoleSelection.Filter(account.GameRoles ?? [], account.ServerType);
            if (Scope != null) _items = _items.DistinctBy(r => r.region).ToList();
            RoleBox.ItemsSource = _items.Select(role => Scope == null ? GameRoleDisplay.BoundRole(role) :
                GameRoleDisplay.ServerName(role.region, role.region_name)).ToList();
            var selected = account == null ? null : Scope == null ? GameRoleSelection.Current(account) : Scope.Current(account);
            RoleBox.SelectedIndex = _items.FindIndex(r => r.game_uid == selected?.game_uid && r.region == selected?.region);
            RoleBox.IsEnabled = _items.Count > 0;
            if (RoleBox.SelectedIndex >= 0) ErrorText.Visibility = Visibility.Collapsed;
        }
        finally { _updating = false; }
    }

    private async Task LoadAsync(bool refresh)
    {
        var version = ++_loadVersion;
        UpdateItems();
        ErrorText.Visibility = Visibility.Collapsed;
        try
        {
            if (refresh) await _roles.RefreshAsync();
            if (version != _loadVersion || !IsLoaded) return;
            UpdateItems();
            if (_accounts.GetActiveAccountEntry() != null && RoleBox.SelectedIndex < 0)
                ShowError("GameRole_Unavailable".GetLocalized());
        }
        catch (Exception ex)
        {
            if (version == _loadVersion && IsLoaded) ShowError(ex.Message);
        }
    }

    private async void RoleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updating || RoleBox.SelectedIndex < 0 || _accountId == null) return;
        var role = _items[RoleBox.SelectedIndex];
        try
        {
            await _roles.SelectAsync(_accountId, role, Scope);
            ErrorText.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) { UpdateItems(); ShowError(ex.Message); }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync(true);
    private void ShowError(string error)
    {
        ErrorText.Text = error;
        ErrorText.Visibility = Visibility.Visible;
    }
}
