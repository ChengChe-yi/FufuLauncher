/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services;

public sealed class GachaAuthKeyException(string message, int? returnCode = null, bool requiresReLogin = false) : Exception(message)
{
    public int? ReturnCode { get; } = returnCode;
    public bool RequiresReLogin => requiresReLogin;
}
