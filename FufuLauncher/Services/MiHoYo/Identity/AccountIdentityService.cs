/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Diagnostics;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Models.MiHoYo.Identity;
using FufuLauncher.Services.MiHoYo.Fingerprint;

namespace FufuLauncher.Services.MiHoYo;

public sealed class AccountIdentityService
{
    private readonly AccountManager _accountManager;
    private readonly DeviceFpService _deviceFpService;

    public AccountIdentityService(AccountManager accountManager, DeviceFpService deviceFpService)
    {
        _accountManager = accountManager;
        _deviceFpService = deviceFpService;
    }
    
    public async Task<AccountContext> BuildAsync(string accountId)
    {
        var cookies = await _accountManager.LoadCookiesAsync(accountId);
        if (cookies == null)
        {
            cookies = new Dictionary<string, string>();
            Debug.WriteLine($"[AccountIdentity] 账号 {accountId} 未找到 cookies，返回空 ctx");
        }
        
        var fpRequest = await _deviceFpService.GetFingerprintRequestAsync(accountId);
        if (fpRequest is null || string.IsNullOrEmpty(fpRequest.DeviceFp))
            throw new InvalidOperationException($"账号 {accountId} 设备指纹不可用（注册失败）");
        
        var serverType = ServerTypeExtensions.ParseServerType(ExtractServerType(accountId));
        var accountIdentity = new AccountIdentity(
            Stuid: ExtractStuid(cookies, serverType),
            Mid: cookies.GetValueOrDefault("mid") ?? "");
        
        const string model = "2605EPN8EC";
        const string sysVersion = "16";
        const string buildId = "V417IR";
        var device = new DeviceIdentity(
            DeviceId: fpRequest.DeviceId,
            BbsDeviceId: fpRequest.BbsDeviceId ?? "",
            DeviceFp: fpRequest.DeviceFp ?? "",
            DeviceName: "Xiaomi " + model,
            SysVersion: sysVersion,
            Model: model,
            FpLastUpdate: DateTimeOffset.UtcNow);

        var ua = new UserAgent(
            Mobile: string.Format(UserAgents.AndroidBbsTemplate, sysVersion, model, buildId, HeaderVersions.MobileCnLogin),
            OkHttp: UserAgents.OkHttp);

        return new AccountContext(
            AccountId: accountId,
            ServerType: serverType,
            Cookies: cookies,
            Identity: accountIdentity,
            Device: device,
            UserAgent: ua);
    }

    private static string ExtractServerType(string accountId)
    {
        var idx = accountId.IndexOf('_');
        return idx > 0 ? accountId[..idx] : "cn";
    }

    private static string ExtractStuid(Dictionary<string, string> cookies, ServerType serverType)
    {
        if (serverType == ServerType.Cn)
        {
            if (cookies.TryGetValue("ltuid", out var ltuid) && !string.IsNullOrEmpty(ltuid))
                return ltuid;
            if (cookies.TryGetValue("stuid", out var stuid) && !string.IsNullOrEmpty(stuid))
                return stuid;
        }
        else
        {
            if (cookies.TryGetValue("ltuid_v2", out var ltuidV2) && !string.IsNullOrEmpty(ltuidV2))
                return ltuidV2;
            if (cookies.TryGetValue("stuid", out var stuid) && !string.IsNullOrEmpty(stuid))
                return stuid;
        }
        return "";
    }
}
