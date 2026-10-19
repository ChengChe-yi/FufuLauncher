/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace FufuLauncher.Views;

public sealed partial class SettingsPage
{
    private void OnCheckinAccountDragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        e.Data.RequestedOperation = DataPackageOperation.Move;
    }

    private async void OnCheckinAccountDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs e)
    {
        if (e.DropResult == DataPackageOperation.Move)
            await ViewModel.SaveCheckinAccountOrderAsync();
    }
}