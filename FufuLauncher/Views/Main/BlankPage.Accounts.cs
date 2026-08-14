/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FufuLauncher.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using File = System.IO.File;

namespace FufuLauncher.Views;

public sealed partial class BlankPage
{
    #region 账号管理（多账号切换）

    private async Task LoadAccountsAsync()
    {
        try
        {
            if (!File.Exists(_accountsFilePath))
            {
                DispatcherQueue.TryEnqueue(() => AccountsListView.ItemsSource = new List<GameAccountData>());
                return;
            }

            var json = await File.ReadAllTextAsync(_accountsFilePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                DispatcherQueue.TryEnqueue(() => AccountsListView.ItemsSource = new List<GameAccountData>());
                return;
            }

            List<GameAccountData>? accounts;
            try
            {
                accounts = JsonSerializer.Deserialize<List<GameAccountData>>(json);
            }
            catch
            {
                try { File.Delete(_accountsFilePath); }
                catch
                {
                    // ignored
                }

                DispatcherQueue.TryEnqueue(() => AccountsListView.ItemsSource = new List<GameAccountData>());
                return;
            }

            DispatcherQueue.TryEnqueue(() => AccountsListView.ItemsSource = accounts ?? new List<GameAccountData>());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[LoadAccountsAsync] 失败: {ex.Message}");
            DispatcherQueue.TryEnqueue(() => AccountsListView.ItemsSource = new List<GameAccountData>());
        }
    }

    private async void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\miHoYo\原神");
            if (key == null) { await ShowError("Err_CannotAccessRegistry".GetLocalized()); return; }

            var sdkData = key.GetValue("MIHOYOSDK_ADL_PROD_CN_h3123967166") as byte[];
            if (sdkData == null) { await ShowError("Err_NoLoggedInAccount".GetLocalized()); return; }

            int nullIndex = Array.IndexOf(sdkData, (byte)0);
            int length = nullIndex >= 0 ? nullIndex : sdkData.Length;
            var sdkString = Encoding.UTF8.GetString(sdkData, 0, length);

            var accounts = await LoadAccountsFromFileAsync();
            if (accounts.Any(a => a.SdkData == sdkString))
            {
                await ShowError("Err_AccountAlreadySaved".GetLocalized());
                return;
            }

            var inputTextBox = new TextBox
            {
                PlaceholderText = "Placeholder_EnterAccountName".GetLocalized(),
                MaxLength = 20,
                VerticalAlignment = VerticalAlignment.Center
            };

            var dialog = new ContentDialog
            {
                Title = "Title_SaveNewAccount".GetLocalized(),
                Content = inputTextBox,
                PrimaryButtonText = "SaveBtn".GetLocalized(),
                CloseButtonText = "CancelBtn".GetLocalized(),
                XamlRoot = XamlRoot,
                DefaultButton = ContentDialogButton.Primary
            };

            var result = await dialog.ShowAsync();

            if (result != ContentDialogResult.Primary) return;

            string accountName = inputTextBox.Text.Trim();
            if (string.IsNullOrEmpty(accountName))
            {
                accountName = string.Format("Prefix_Account_Format".GetLocalized(), DateTime.Now.ToString("MMdd_HHmmss"));
            }

            accounts.Add(new GameAccountData
            {
                Id = Guid.NewGuid(),
                Name = accountName,
                SdkData = sdkString,
                LastUsed = DateTime.Now
            });

            await SaveAccountsToFileAsync(accounts);
            await LoadAccountsAsync();

            Debug.WriteLine($"[AddAccount_Click] 成功保存账号: {accountName}");
        }
        catch (Exception ex)
        {
            await ShowError(string.Format("Err_SaveFailed_Format".GetLocalized(), ex.Message));
        }
    }

    private async void RefreshAccounts_Click(object sender, RoutedEventArgs e) => await LoadAccountsAsync();

    private async void SwitchAccount_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as Button)?.Tag is not GameAccountData account) return;

            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\miHoYo\原神");
            if (key == null) { await ShowError("Err_CannotAccessRegistry".GetLocalized()); return; }

            var sdkBytes = Encoding.UTF8.GetBytes(account.SdkData);
            var target = new byte[sdkBytes.Length + 1];
            Array.Copy(sdkBytes, target, sdkBytes.Length);
            target[sdkBytes.Length] = 0;

            key.SetValue("MIHOYOSDK_ADL_PROD_CN_h3123967166", target, Microsoft.Win32.RegistryValueKind.Binary);

            await UpdateAccountLastUsedAsync(account.Id);
            await LoadAccountsAsync();

            var successDialog = new ContentDialog
            {
                Title = "Title_SwitchSuccess".GetLocalized(),
                Content = string.Format("Msg_SwitchedToAccount_Format".GetLocalized(), account.Name),
                PrimaryButtonText = "Btn_GotIt".GetLocalized(),
                XamlRoot = this.XamlRoot
            };
            await successDialog.ShowAsync();

            Debug.WriteLine($"[SwitchAccount_Click] 账号切换成功: {account.Name}");
        }
        catch (Exception ex)
        {
            await ShowError(string.Format("Err_SwitchFailed_Format".GetLocalized(), ex.Message));
        }
    }

    private async void DeleteAccount_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if ((sender as Button)?.Tag is not GameAccountData account) return;

            var dialog = new ContentDialog
            {
                Title = "Title_ConfirmDelete".GetLocalized(),
                Content = string.Format("Msg_DeleteAccountConfirm_Format".GetLocalized(), account.Name),
                PrimaryButtonText = "DeleteLabel".GetLocalized(),
                CloseButtonText = "CancelBtn".GetLocalized(),
                XamlRoot = this.XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var accounts = await LoadAccountsFromFileAsync();
            accounts.RemoveAll(a => a.Id == account.Id);
            await SaveAccountsToFileAsync(accounts);
            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            await ShowError(string.Format("Err_DeleteFailed_Format".GetLocalized(), ex.Message));
        }
    }

    private async Task UpdateAccountLastUsedAsync(Guid id)
    {
        try
        {
            var accounts = await LoadAccountsFromFileAsync();
            var account = accounts.FirstOrDefault(a => a.Id == id);
            if (account != null)
            {
                account.LastUsed = DateTime.Now;
                await SaveAccountsToFileAsync(accounts);
            }
        }
        catch { }
    }

    private async Task<List<GameAccountData>> LoadAccountsFromFileAsync()
    {
        try
        {
            if (!File.Exists(_accountsFilePath)) return new List<GameAccountData>();
            var json = await File.ReadAllTextAsync(_accountsFilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<List<GameAccountData>>(json) ?? new List<GameAccountData>();
        }
        catch { return new List<GameAccountData>(); }
    }

    private async Task SaveAccountsToFileAsync(List<GameAccountData> accounts)
    {
        try
        {
            var dir = Path.GetDirectoryName(_accountsFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var options = new JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(_accountsFilePath, JsonSerializer.Serialize(accounts, options), Encoding.UTF8);
        }
        catch (Exception ex) { Debug.WriteLine($"[SaveAccountsToFileAsync] 失败: {ex.Message}"); }
    }

    #endregion
}
