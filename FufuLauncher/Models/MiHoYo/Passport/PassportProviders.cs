/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Models.MiHoYo.Passport;

public interface IAigisProvider
{
    string? Aigis { get; set; }
}

public interface IVerifyProvider
{
    string? Verify { get; set; }
}

public interface IPassportPasswordProvider : IAigisProvider, IVerifyProvider
{
    string? Account { get; }
    string? Password { get; }
}

public interface IPassportMobileCaptchaProvider : IAigisProvider
{
    string? ActionType { get; }
    string? Mobile { get; }
    string? Captcha { get; }
}
