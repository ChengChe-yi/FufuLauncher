/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Globalization;
using FufuLauncher.Helpers;

namespace FufuLauncher.ViewModels;

public sealed partial class DataViewModel
{
    #region Formatting & Localization

    private static (string text, string tag) ScoreToTier(double score) => score switch
    {
        >= 78 => ("T0", "s1"),
        >= 60 => ("T1", "s"),
        >= 42 => ("T2", "a"),
        >= 22 => ("T3", "b"),
        _ => ("T4", "f")
    };

    private static string NormalizeTierTag(string? rankClass) => rankClass?.ToLowerInvariant() switch
    {
        "s1" => "s1",
        "s" => "s",
        "a" => "a",
        "b" => "b",
        _ => "f"
    };

    private static string RankClassText(string? rankClass) => rankClass?.ToLowerInvariant() switch
    {
        "s1" => "S+",
        "s" => "S",
        "a" => "A",
        "b" => "B",
        "f" => "C",
        _ => Dash
    };

    private static string TierDescription(string? rankClass) => rankClass?.ToLowerInvariant() switch
    {
        "s1" => L("DataPage_TierS1Desc"),
        "s" => L("DataPage_TierSDesc"),
        "a" => L("DataPage_TierADesc"),
        "b" => L("DataPage_TierBDesc"),
        _ => L("DataPage_TierCDesc")
    };

    private static (DateTime? start, DateTime? end) ParseRange(string? range)
    {
        if (string.IsNullOrWhiteSpace(range)) return (null, null);

        var parts = range.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return (null, null);

        return (ParseDate(parts[0]), parts.Length > 1 ? ParseDate(parts[1]) : null);
    }

    private static DateTime? ParseDate(string text)
    {
        string[] formats = { "yyyy/MM/dd", "yyyy/M/d", "yyyy-MM-dd", "yyyy.MM.dd" };
        if (DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture, DateTimeStyles.None,
                out var exact))
        {
            return exact;
        }

        return DateTime.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var loose)
            ? loose
            : null;
    }

    private static string CleanVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version)) return Dash;

        var index = version.IndexOfAny(new[] { ':', '：' });
        return index >= 0 && index < version.Length - 1 ? version[(index + 1)..].Trim() : version.Trim();
    }

    private static string Fmt(double? value, int decimals)
        => value.HasValue ? value.Value.ToString("F" + decimals, CultureInfo.InvariantCulture) : Dash;

    private static string PctText(double? value)
        => value.HasValue ? value.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%" : Dash;

    private static string SignedPct(double? value)
        => value.HasValue
            ? (value.Value > 0 ? "+" : string.Empty) + value.Value.ToString("0.#", CultureInfo.InvariantCulture) + "%"
            : Dash;

    private static string NumText(double? value)
        => value.HasValue ? value.Value.ToString("N0", CultureInfo.CurrentCulture) : Dash;

    private static string Compact(double? value)
    {
        if (!value.HasValue) return Dash;
        var v = value.Value;

        return Math.Abs(v) switch
        {
            >= 1_000_000_000 => (v / 1_000_000_000).ToString("0.##", CultureInfo.InvariantCulture) + "B",
            >= 1_000_000 => (v / 1_000_000).ToString("0.##", CultureInfo.InvariantCulture) + "M",
            >= 10_000 => (v / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "K",
            _ => v.ToString("N0", CultureInfo.CurrentCulture)
        };
    }

    private static string ListSep => L("DataPage_ListSeparator");

    private static string ClauseSep => L("DataPage_ClauseSeparator");

    private static string L(string key) => key.GetLocalized();

    private static string LF(string key, params object?[] args)
    {
        var template = key.GetLocalized();
        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    #endregion
}
