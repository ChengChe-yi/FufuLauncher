/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FufuLauncher.Contracts.Services;
using FufuLauncher.Services;

namespace FufuLauncher.ViewModels
{
    public partial class OtherViewModel : ObservableObject
    {
        private readonly ILocalSettingsService _localSettingsService;
        private readonly IAutoClickerService _autoClickerService;
        private readonly Microsoft.UI.Dispatching.DispatcherQueue _dispatcherQueue;
        private bool _isInitializing;
        private bool _isReverting;
        public IRelayCommand OpenBrowserCommand { get; }

        [ObservableProperty] private bool _isAdditionalProgramEnabled;
        [ObservableProperty] private string _additionalProgramPath = string.Empty;
        [ObservableProperty] private string _statusMessage = string.Empty;

        [ObservableProperty] private bool _isAutoClickerEnabled;
        [ObservableProperty] private bool _isMouseLeftClickerEnabled;
        [ObservableProperty] private bool _isMouseRightClickerEnabled;
        [ObservableProperty] private string _triggerKey = "F";
        [ObservableProperty] private string _clickKey = "F";
        [ObservableProperty] private string _stopKey = string.Empty;
        [ObservableProperty] private bool _isRecordingTriggerKey;
        [ObservableProperty] private bool _isRecordingClickKey;
        [ObservableProperty] private bool _isRecordingStopKey;
        [ObservableProperty]
        private bool _isApplyButtonEnabled;

        public IAsyncRelayCommand BrowseProgramCommand
        {
            get;
        }
        public IAsyncRelayCommand SaveSettingsCommand
        {
            get;
        }
        public IRelayCommand RecordTriggerKeyCommand
        {
            get;
        }
        public IRelayCommand RecordClickKeyCommand
        {
            get;
        }
        public IRelayCommand RecordStopKeyCommand
        {
            get;
        }
        public IAsyncRelayCommand ApplyProgramPathCommand
        {
            get;
        }

        public OtherViewModel(ILocalSettingsService localSettingsService, IAutoClickerService autoClickerService)
        {
            _localSettingsService = localSettingsService;
            _autoClickerService = autoClickerService;
            _dispatcherQueue = App.MainWindow.DispatcherQueue;

            BrowseProgramCommand = new AsyncRelayCommand(BrowseProgramAsync);
            SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync);
            RecordTriggerKeyCommand = new RelayCommand(StartRecordingTriggerKey);
            RecordClickKeyCommand = new RelayCommand(StartRecordingClickKey);
            RecordStopKeyCommand = new RelayCommand(StartRecordingStopKey);
            ApplyProgramPathCommand = new AsyncRelayCommand(ApplyProgramPathAsync);
            OpenBrowserCommand = new RelayCommand(OpenBrowserWindow);
            _autoClickerService.IsEnabledChanged += AutoClickerService_IsEnabledChanged;

            LoadSettings();
        }
    }
}
