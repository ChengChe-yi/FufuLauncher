/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Models.MiHoYo.Passport;
using FufuLauncher.Services;
using FufuLauncher.Services.MiHoYo.Passport;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class OverseaPasswordLoginDialog : ContentDialog, IPassportPasswordProvider
{
    private const int MaxAttempts = 3;

    private readonly OverseaPassportClient _passportClient = App.GetService<OverseaPassportClient>();
    private readonly GeetestService _geetestService = App.GetService<GeetestService>();
    private readonly OverseaRiskVerificationService _riskVerificationService = App.GetService<OverseaRiskVerificationService>();
    
    public string? Account => AccountTextBox?.Text?.Trim();
    
    public string? Password => PasswordBox?.Password;
    
    public string? Aigis
    {
        get; set;
    }
    
    public string? Verify
    {
        get; set;
    }

    public OverseaPasswordLoginDialog()
    {
        InitializeComponent();
        IsPrimaryButtonEnabled = false;
    }
    
    public async Task<(LoginResult? Result, string? Error)> ShowAndLoginAsync(XamlRoot xamlRoot)
    {
        XamlRoot = xamlRoot;
        if (await ShowAsync() != ContentDialogResult.Primary)
        {
            return (null, null);
        }

        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            (string? rawAigis, string? rawRisk, PassportResponse<LoginResult> response) =
                await _passportClient.LoginByPasswordAsync(Account!, Password!, Aigis, Verify);

            System.Diagnostics.Debug.WriteLine(
                $"[OverseaPasswordLogin] 第 {attempt + 1} 次尝试: retcode={response.RetCode}, aigis={(rawAigis is null ? "无" : "有")}, risk={(rawRisk is null ? "无" : "有")}");

            if (await _geetestService.TryVerifyAigisSessionAsync(this, rawAigis, isOversea: true))
            {
                System.Diagnostics.Debug.WriteLine($"[OverseaPasswordLogin] 极验完成，携带 aigis 重试");
                continue;
            }

            if (!string.IsNullOrEmpty(rawRisk))
            {
                if (await _riskVerificationService.TryVerifyAsync(this, rawRisk, xamlRoot))
                {
                    continue;
                }
                
                return (null, "安全验证未完成");
            }

            if (response.IsSuccess && response.Data is not null)
            {
                return (response.Data, null);
            }

            if (!string.IsNullOrEmpty(Aigis))
            {
                System.Diagnostics.Debug.WriteLine($"[OverseaPasswordLogin] aigis 被拒绝(retcode={response.RetCode})，清空后重新验证");
                Aigis = null;
                continue;
            }

            return (null, $"[{response.RetCode}] {response.Message}");
        }

        return (null, "登录失败：校验次数过多，请稍后重试");
    }

    private void AccountTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdatePrimaryButtonState();

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) => UpdatePrimaryButtonState();

    private void UpdatePrimaryButtonState()
    {
        IsPrimaryButtonEnabled = !string.IsNullOrEmpty(Account) && !string.IsNullOrEmpty(Password);
    }
}
