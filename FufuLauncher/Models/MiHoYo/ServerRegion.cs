/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Models.MiHoYo;

public static class ServerRegion
{
    public const string CnGf01 = "cn_gf01";
    public const string CnQd01 = "cn_qd01";
    public const string OsUsa = "os_usa";
    public const string OsEuro = "os_euro";
    public const string OsAsia = "os_asia";
    public const string OsCht = "os_cht";
    
    public static string Resolve(string uid)
    {
        if (string.IsNullOrEmpty(uid) || uid.Length < 9)
            return CnGf01;

        return uid.AsSpan()[^9] switch
        {
            >= '1' and <= '4' => CnGf01,
            '5' => CnQd01,
            '6' => OsUsa,
            '7' => OsEuro,
            '8' => OsAsia,
            '9' => OsCht,
            _ => CnGf01,
        };
    }
    
    public static bool IsOversea(string server) =>
        server.StartsWith("os_", StringComparison.OrdinalIgnoreCase);
}
