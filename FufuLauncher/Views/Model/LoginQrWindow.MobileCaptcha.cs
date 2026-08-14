/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Models.MiHoYo.Passport;
using FufuLauncher.Services.MiHoYo.Passport;
using Microsoft.UI.Xaml.Controls;

namespace FufuLauncher.Views;

public sealed partial class LoginQrWindow
{
    #region 手机验证码登录
    private async Task StartMobileCaptchaLoginAsync()
    {
        _isLoginCompleting = false;
        _currentSession?.Cancel();
        UpdateStatus("", false, true);

        var dialog = new MobileCaptchaDialog { XamlRoot = Content?.XamlRoot };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        if (string.IsNullOrEmpty(dialog.ActionType) || string.IsNullOrEmpty(dialog.Mobile) || string.IsNullOrEmpty(dialog.Captcha))
        {
            UpdateStatus("请先获取短信验证码", false);
            return;
        }

        UpdateStatus("正在登录...", true);
        var passportClient = App.GetService<PassportClient>();
        PassportResponse<LoginResult> response =
            await passportClient.LoginByMobileCaptchaAsync(dialog.ActionType, dialog.Mobile, dialog.Captcha, dialog.Aigis);

        if (!response.IsSuccess || response.Data?.Token?.Token == null || response.Data.UserInfo == null)
        {
            UpdateStatus($"登录失败: [{response.RetCode}] {response.Message}", false);
            return;
        }

        string stoken = response.Data.Token.Token;
        string mid = response.Data.UserInfo.Mid;
        string aid = response.Data.UserInfo.Aid;

        if (string.IsNullOrEmpty(stoken) || string.IsNullOrEmpty(mid))
        {
            UpdateStatus("登录失败: 响应缺少凭证", false);
            return;
        }

        var cookies = await ExchangeV2TokensAsync(stoken, mid, aid);
        if (cookies != null)
        {
            OnLoginSuccess(cookies, "cn");
        }
    }
    #endregion
}
