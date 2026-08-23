/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Services.Yae;

/// <summary>Yae 组件注入失败。</summary>
internal sealed class YaeInjectionException : Exception
{
    public YaeInjectionException(string message, Exception? inner = null) : base(message, inner) { }
}
