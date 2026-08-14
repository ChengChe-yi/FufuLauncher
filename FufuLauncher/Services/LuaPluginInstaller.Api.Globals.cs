/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Reflection;
using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region Lua Globals

    private void RegisterGlobalHandlers(Script script)
    {
        script.Globals["tostring"] = (Func<DynValue, string>)(value =>
        {
            if (value.IsNil()) return "nil";
            if (value.Type == DataType.String) return value.String ?? "";
            if (value.Type == DataType.Number) return value.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (value.Type == DataType.Boolean) return value.Boolean ? "true" : "false";
            if (value.Type == DataType.Function) return "(function)";
            if (value.Type == DataType.Table) return "(table)";
            if (value.Type == DataType.UserData) return "(userdata)";
            if (value.Type == DataType.Thread) return "(thread)";
            var ps = value.ToPrintString();
            return ps ?? "";
        });

        script.Globals["print"] = (Action<DynValue>)(value =>
        {
            var str = value.IsNil() ? "nil" :
                value.Type == DataType.String ? value.String :
                value.ToPrintString();
            LogMessage($"[Lua print] {str}");
        });

        script.Globals["pcall"] = (Func<DynValue, DynValue>)(fn =>
        {
            if (fn.IsNil() || fn.Type != DataType.Function)
                return DynValue.True;

            try
            {
                script.Call(fn);
            }
            catch (Exception ex)
            {
                string msg;
                try
                {
                    if (ex is InterpreterException iex)
                        msg = iex.Message ?? iex.GetType().Name;
                    else if (ex is TargetInvocationException tie && tie.InnerException != null)
                        msg = tie.InnerException.Message ?? tie.InnerException.GetType().Name;
                    else
                        msg = ex.Message ?? ex.GetType().Name;
                }
                catch { msg = "Unknown error"; }
                LogMessage($"pcall caught: {msg}");
            }

            return DynValue.True;
        });
    }

    #endregion
}
