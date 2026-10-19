/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Views;

namespace FufuLauncher.ViewModels;

public partial class AccountViewModel
{
    private MiyousheQrWindow? _miyousheQrWindow;

    [RelayCommand]
    private void OpenMiyousheQr()
    {
        if (_miyousheQrWindow == null)
        {
            _miyousheQrWindow = new MiyousheQrWindow();
            _miyousheQrWindow.Closed += (_, _) => _miyousheQrWindow = null;
        }

        _miyousheQrWindow.Activate();
    }
}