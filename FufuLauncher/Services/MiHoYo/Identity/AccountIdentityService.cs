/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Diagnostics;
using FufuLauncher.Constants.MiHoYo;
using FufuLauncher.Models.MiHoYo.Identity;
using FufuLauncher.Services.MiHoYo.Fingerprint;

namespace FufuLauncher.Services.MiHoYo;

/// <summary>
/// 单账号身份聚合入口：把 cookies + 设备指纹 + UA 装成不可变 <see cref="AccountContext"/>。
/// 业务 service 应通过它取得身份信息，而不是直接依赖 <see cref="DeviceFpService"/> / <see cref="AccountManager"/>。
/// </summary>
public sealed class AccountIdentityService
{
    private readonly AccountManager _accountManager;
    private readonly DeviceFpService _deviceFpService;

    public AccountIdentityService(AccountManager accountManager, DeviceFpService deviceFpService)
    {
        _accountManager = accountManager;
        _deviceFpService = deviceFpService;
    }

    /// <summary>
    /// 为账号构建完整身份上下文：读 cookies → 确保指纹已注册/读取 → 派生 device_id / bbs_device_id / UA → 装成 <see cref="AccountContext"/>。
    /// 不写盘；指纹注册由 <see cref="DeviceFpService"/> 内部完成并持久化。
    /// </summary>
    public async Task<AccountContext> BuildAsync(string accountId)
    {
        // 1. 读 cookies（账号文件不存在 / 解析失败：返回空字典，由调用方决定如何处理）
        var cookies = await _accountManager.LoadCookiesAsync(accountId);
        if (cookies == null)
        {
            cookies = new Dictionary<string, string>();
            Debug.WriteLine($"[AccountIdentity] 账号 {accountId} 未找到 cookies，返回空 ctx");
        }

        // 2. 确保指纹已注册/读取（同账号串行；返回完整请求体，含 device_id / bbs_device_id）
        var fpRequest = await _deviceFpService.GetFingerprintRequestAsync(accountId);
        if (fpRequest is null || string.IsNullOrEmpty(fpRequest.DeviceFp))
            throw new InvalidOperationException($"账号 {accountId} 设备指纹不可用（注册失败）");

        // 4. 派生身份字段
        var serverType = ServerTypeExtensions.ParseServerType(ExtractServerType(accountId));
        var accountIdentity = new AccountIdentity(
            Stuid: ExtractStuid(cookies, serverType),
            Mid: cookies.GetValueOrDefault("mid") ?? "");

        // 设备画像三处必须自洽：DeviceName（未编码）/ SysVersion / UA 同源，否则服务端设备画像校验会触发 1034
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
