/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Globalization;
using System.Text.RegularExpressions;
using System.Web;

namespace FufuLauncher.Helpers;

public sealed class GachaLink
{
    public required string ApiUrl
    {
        get;
        init;
    }

    public string? Region
    {
        get;
        init;
    }

    public DateTimeOffset? CreatedAt
    {
        get;
        init;
    }
}

public static class GachaUrlHelper
{
    // Cache URLs are ASCII (including percent-encoded authkeys), surrounded by binary data.
    internal static readonly Regex UrlPattern = new(
        @"https://[^\x00-\x20\x7f-\xff""'<>\\]+",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static GachaLink? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        foreach (Match match in UrlPattern.Matches(text))
        {
            var link = ParseUrl(match.Value);
            if (link != null) return link;
        }

        return null;
    }

    private static GachaLink? ParseUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            return null;

        var host = uri.Host.ToLowerInvariant();
        var isOversea = host is "hk4e-api-os.hoyoverse.com" or "hk4e-api-os.mihoyo.com"
            or "public-operation-hk4e-sg.hoyoverse.com" or "webstatic-sea.hoyoverse.com"
            or "webstatic-sea.mihoyo.com" or "gs.hoyoverse.com";
        var isApi = host is "public-operation-hk4e.mihoyo.com" or "hk4e-api.mihoyo.com"
            or "hk4e-api.miyoushe.com" or "hk4e-api-os.hoyoverse.com" or "hk4e-api-os.mihoyo.com"
            or "public-operation-hk4e-sg.hoyoverse.com";
        var isPage = host is "webstatic.mihoyo.com" or "webstatic.miyoushe.com"
            or "webstatic-sea.hoyoverse.com" or "webstatic-sea.mihoyo.com" or "gs.hoyoverse.com";

        string endpoint;
        if (isApi && uri.AbsolutePath.EndsWith("/gacha_info/api/getGachaLog", StringComparison.Ordinal))
            endpoint = uri.GetLeftPart(UriPartial.Path);
        else if (isPage && uri.AbsolutePath.Contains("e20190909gacha-v", StringComparison.Ordinal) &&
                 uri.AbsolutePath.EndsWith("/index.html", StringComparison.Ordinal))
            endpoint = isOversea
                ? "https://hk4e-api-os.hoyoverse.com/gacha_info/api/getGachaLog"
                : "https://public-operation-hk4e.mihoyo.com/gacha_info/api/getGachaLog";
        else
            return null;

        var query = HttpUtility.ParseQueryString(uri.Query);
        var authkey = query["authkey"];
        if (query.GetValues("authkey")?.Length != 1 || string.IsNullOrWhiteSpace(authkey) ||
            authkey.Any(char.IsControl)) return null;

        var region = query["region"];
        if (!string.IsNullOrEmpty(region))
        {
            if (query.GetValues("region")?.Length != 1 || region is not
                    ("cn_gf01" or "cn_qd01" or "os_usa" or "os_euro" or "os_asia" or "os_cht"))
                return null;
            if (isOversea != region.StartsWith("os_", StringComparison.Ordinal)) return null;
        }

        var authkeyVersion = query["authkey_ver"] ?? "1";
        var signType = query["sign_type"] ?? "2";
        if (!int.TryParse(authkeyVersion, out var version) || version <= 0 ||
            !int.TryParse(signType, out var sign) || sign <= 0) return null;

        var normalized = HttpUtility.ParseQueryString(string.Empty);
        normalized["authkey"] = authkey;
        normalized["authkey_ver"] = authkeyVersion;
        normalized["sign_type"] = signType;
        normalized["lang"] = query["lang"] ?? "zh-cn";
        if (!string.IsNullOrEmpty(region)) normalized["region"] = region;

        DateTimeOffset? createdAt = null;
        if (long.TryParse(query["timestamp"], NumberStyles.None, CultureInfo.InvariantCulture, out var timestamp) &&
            timestamp is > 0 and <= 253402300799)
            createdAt = DateTimeOffset.FromUnixTimeSeconds(timestamp);

        return new GachaLink { ApiUrl = $"{endpoint}?{normalized}", Region = region, CreatedAt = createdAt };
    }
}