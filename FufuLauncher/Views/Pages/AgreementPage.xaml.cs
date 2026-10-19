/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Services.CodeSigning;
using FufuLauncher.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace FufuLauncher.Views
{
    public sealed partial class AgreementPage : Page
    {
        public AgreementViewModel ViewModel
        {
            get;
        }

        public AgreementPage()
        {
            ViewModel = App.GetService<AgreementViewModel>();
            InitializeComponent();
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.IsIconCheckMode)
            {
                EntranceStoryboard.Begin();
            }
        }

        private void OnAgreementScrollChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (ViewModel.HasReadAgreement) return;
            var sv = (ScrollViewer)sender;
            if (sv.VerticalOffset >= sv.ScrollableHeight - 10)
            {
                ViewModel.HasReadAgreement = true;
            }
        }

        private async void OnTrustInstallClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = App.GetService<CodeSigningTrustService>();
                var result = await TrustInstallFlow.InstallAsync(service, XamlRoot, TrustStoreScope.CurrentUser);

                if (result == null) return;

                TrustStatusText.Text = result.Message;

                if (!result.Ok) return;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Agreement] 安装平台证书失败: {ex}");
                TrustStatusText.Text = $"安装失败：{ex.Message}";
                return;
            }

            await ViewModel.FinishOnboardingAsync();
        }

        private async void OnTrustSkipClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.FinishOnboardingAsync();
        }
    }
}