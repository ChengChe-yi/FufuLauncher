/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.Helpers;
using MoonSharp.Interpreter;

namespace FufuLauncher.Services;

public partial class LuaPluginInstaller
{
    #region System Info API

    private void RegisterSystemHandlers(Script script, Table sysTable)
    {
        sysTable["get_gpu"] = (Func<DynValue>)(() =>
        {
            var info = SystemEnvironmentHelper.GetGpuInfo();
            var table = new Table(script);
            table["name"] = DynValue.NewString(info.Name);
            table["vendor"] = DynValue.NewString(info.Vendor);
            table["family"] = DynValue.NewString(info.Family);
            table["series"] = DynValue.NewString(info.Series);
            table["raw"] = DynValue.NewString(info.Name);
            return DynValue.NewTable(table);
        });

        sysTable["gpu_matches"] = (Func<DynValue, bool>)(rule =>
        {
            try
            {
                var info = SystemEnvironmentHelper.GetGpuInfo();
                if (rule.IsNil() || rule.Type != DataType.Table)
                {
                    return true;
                }

                var dict = TableToDict(rule, "gpu_matches");
                if (dict.Count == 0)
                {
                    return true;
                }
                return MatchesGpuRule(info, dict);
            }
            catch (Exception ex)
            {
                LogMessage($"gpu_matches 执行出错: {ex.Message}");
                return false;
            }
        });

        sysTable["gpu_matches_any"] = (Func<DynValue, bool>)(rules =>
        {
            try
            {
                var info = SystemEnvironmentHelper.GetGpuInfo();
                if (rules.IsNil() || rules.Type != DataType.Table)
                {
                    return true;
                }

                var anyRule = false;
                foreach (var pair in rules.Table.Pairs)
                {
                    var ruleValue = pair.Value;
                    if (ruleValue.IsNil() || ruleValue.Type != DataType.Table)
                    {
                        continue;
                    }

                    anyRule = true;
                    var dict = TableToDict(ruleValue, "gpu_matches_any rule");
                    if (MatchesGpuRule(info, dict))
                    {
                        return true;
                    }
                }
                return !anyRule;
            }
            catch (Exception ex)
            {
                LogMessage($"gpu_matches_any 执行出错: {ex.Message}");
                return false;
            }
        });

        sysTable["get_cpu"] = (Func<DynValue>)(() =>
        {
            var table = new Table(script);
            table["name"] = DynValue.NewString(SystemEnvironmentHelper.GetCpuName());
            return DynValue.NewTable(table);
        });

        sysTable["get_memory"] = (Func<DynValue>)(() =>
        {
            var table = new Table(script);
            table["total_gb"] = DynValue.NewNumber(SystemEnvironmentHelper.GetTotalMemoryGB());
            return DynValue.NewTable(table);
        });

        sysTable["get_os"] = (Func<DynValue>)(() =>
        {
            var table = new Table(script);
            table["version"] = DynValue.NewString(SystemEnvironmentHelper.GetOsVersion());
            return DynValue.NewTable(table);
        });
    }

    private static Dictionary<string, string> TableToDict(DynValue tableValue, string operation)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (tableValue.IsNil())
        {
            return result;
        }

        if (tableValue.Type != DataType.Table)
        {
            throw new InvalidOperationException(
                $"'{operation}' expects a table of string key/value pairs.");
        }

        var table = tableValue.Table;
        foreach (var pair in table.Pairs)
        {
            var key = pair.Key;
            if (key.Type != DataType.String)
            {
                continue;
            }

            var value = pair.Value;
            if (value.IsNil())
            {
                continue;
            }

            string strValue;
            if (value.Type == DataType.String)
            {
                strValue = value.String;
            }
            else
            {
                strValue = value.ToString();
            }

            if (strValue == null)
            {
                continue;
            }

            result[key.String] = strValue;
        }

        return result;
    }

    private static bool MatchesGpuRule(GpuInfo info, IReadOnlyDictionary<string, string> rule)
    {
        if (rule == null || rule.Count == 0)
        {
            return true;
        }

        if (rule.TryGetValue("vendor", out var vendorValue)
            && !string.IsNullOrEmpty(vendorValue)
            && !string.Equals(info.Vendor, vendorValue, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (rule.TryGetValue("family", out var familyValue)
            && !string.IsNullOrEmpty(familyValue))
        {
            var familyOk = !string.IsNullOrEmpty(info.Family)
                           && !string.Equals(info.Family, "Unknown", StringComparison.OrdinalIgnoreCase)
                           && string.Equals(info.Family, familyValue, StringComparison.OrdinalIgnoreCase);
            if (!familyOk)
            {
                familyOk = info.Name != null
                           && info.Name.Contains(familyValue, StringComparison.OrdinalIgnoreCase);
            }
            if (!familyOk) return false;
        }

        if (rule.TryGetValue("series", out var seriesValue)
            && !string.IsNullOrEmpty(seriesValue))
        {
            var seriesOk = !string.IsNullOrEmpty(info.Series)
                           && string.Equals(info.Series, seriesValue, StringComparison.OrdinalIgnoreCase);
            if (!seriesOk)
            {
                seriesOk = info.Name != null
                           && info.Name.Contains(seriesValue, StringComparison.OrdinalIgnoreCase);
            }
            if (!seriesOk) return false;
        }

        if (rule.TryGetValue("name", out var nameValue)
            && !string.IsNullOrEmpty(nameValue))
        {
            if (info.Name == null
                || !info.Name.Contains(nameValue, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    #endregion
}
