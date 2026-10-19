/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using CommunityToolkit.Mvvm.ComponentModel;
using FufuLauncher.Helpers;

namespace FufuLauncher.ViewModels
{
    public partial class CalculatorViewModel : ObservableRecipient
    {
        [ObservableProperty] private bool _isLoading = true;

        [ObservableProperty] private string _statusMessage;

        public CalculatorViewModel()
        {
            _statusMessage = "Common_Initializing".GetLocalized();
        }
    }
}