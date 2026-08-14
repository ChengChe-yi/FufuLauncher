/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;

namespace FufuLauncher.Views;

public sealed partial class BlankPage
{
    #region 账号备注行内编辑

    private void AccountName_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (_currentEditBox != null)
        {
            CancelEdit();
        }

        if (sender is TextBlock textBlock &&
            FindParent<StackPanel>(textBlock) is StackPanel stackPanel &&
            textBlock.DataContext is GameAccountData account)
        {
            _currentTextBlock = textBlock;
            _currentStackPanel = stackPanel;
            _currentAccount = account;

            _currentTextBlock.Visibility = Visibility.Collapsed;

            _currentEditBox = new TextBox
            {
                Text = account.Remark ?? account.Name,
                MinWidth = 100,
                MaxLength = 20,
                VerticalAlignment = VerticalAlignment.Center
            };

            _currentEditBox.KeyDown += EditBox_KeyDown;

            _currentEditBox.LostFocus += (_, _) => CancelEdit();

            int index = stackPanel.Children.IndexOf(textBlock);
            stackPanel.Children.Insert(index, _currentEditBox);

            _currentEditBox.Focus(FocusState.Programmatic);
            _currentEditBox.SelectAll();

            AddHandler(PointerPressedEvent, new PointerEventHandler(Page_PointerPressed), true);
        }
    }

    private void EditBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            CommitEdit();
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            e.Handled = true;
            CancelEdit();
        }
    }

    private async void CommitEdit()
    {
        if (_currentEditBox == null || _currentAccount == null) return;

        string newRemark = _currentEditBox.Text.Trim();

        if (string.IsNullOrEmpty(newRemark) || newRemark == _currentAccount.Name)
        {
            _currentAccount.Remark = null;
        }
        else
        {
            _currentAccount.Remark = newRemark;
        }

        CleanupEditUI();

        try
        {
            var accounts = await LoadAccountsFromFileAsync();

            var accountToUpdate = accounts.FirstOrDefault(a => a.SdkData == _currentAccount.SdkData);
            if (accountToUpdate != null)
            {
                accountToUpdate.Remark = _currentAccount.Remark;
                await SaveAccountsToFileAsync(accounts);
            }

            await LoadAccountsAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存备注失败: {ex.Message}");
        }
    }

    private void CleanupEditUI()
    {
        if (_currentEditBox == null || _currentStackPanel == null || _currentTextBlock == null) return;

        try
        {
            this.RemoveHandler(PointerPressedEvent, new PointerEventHandler(Page_PointerPressed));
            _currentStackPanel.Children.Remove(_currentEditBox);
            _currentTextBlock.Visibility = Visibility.Visible;
        }
        finally
        {
            _currentEditBox = null;
            _currentTextBlock = null;
            _currentStackPanel = null;
            _currentAccount = null;
        }
    }

    private void Page_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_currentEditBox != null)
        {
            var ptr = e.GetCurrentPoint(_currentEditBox);
            if (ptr.Properties.IsLeftButtonPressed)
            {
                if (ptr.Position.X < 0 || ptr.Position.Y < 0 ||
                    ptr.Position.X > _currentEditBox.ActualWidth || ptr.Position.Y > _currentEditBox.ActualHeight)
                {
                    CancelEdit();
                }
            }
        }
    }

    private void CancelEdit()
    {
        CleanupEditUI();
    }

    private T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var current = child;
        while (current != null)
        {
            current = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(current);
            if (current is T typedParent)
                return typedParent;
        }
        return null;
    }

    #endregion
}
