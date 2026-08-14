/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Security.Cryptography;
using System.Text;

namespace MihoyoBBS;

public static class Tools
{
    public static string Md5(string input)
    {
        using (var md5 = MD5.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = md5.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }

    public static string RandomString(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        var random = new Random();
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }

    public static long Timestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static string GetDs(bool web = true)
    {

        var salt = web ? "G1ktdwFL4IyGkHuuWSmz0wUe9Db9scyK" : "idMMaGYmVgPzh3wxmWudUXKUPGidO7GM";
        var t = Timestamp().ToString();
        var r = RandomString(6);
        var c = Md5($"salt={salt}&t={t}&r={r}");
        return $"{t},{r},{c}";
    }

    public static string GetItem(RewardItem item)
    {
        return $"「{item.Name}」x{item.Count}";
    }

    public static string GetDeviceId(string cookie)
    {
        using (var md5 = MD5.Create())
        {
            var bytes = Encoding.UTF8.GetBytes(cookie);
            var hash = md5.ComputeHash(bytes);
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }
    }

    public static string GetUserAgent(string useragent)
    {
        if (string.IsNullOrEmpty(useragent))
        {
            return "Mozilla/5.0 (Linux; Android 12; Unspecified Device) AppleWebKit/537.36 (KHTML, like Gecko) Version/4.0 Chrome/103.0.5060.129 Mobile Safari/537.36 miHoYoBBS/2.93.1";
        }

        useragent = useragent.Replace("; ", " ").Replace(";", " ");

        if (useragent.Contains("miHoYoBBS"))
        {
            int i = useragent.IndexOf("miHoYoBBS");
            if (i > 0 && useragent[i - 1] == ' ')
                i = i - 1;
            return $"{useragent.Substring(0, i)} miHoYoBBS/2.93.1";
        }

        return $"{useragent} miHoYoBBS/2.93.1";
    }

    public static string TidyCookie(string cookies)
    {
        var cookieDict = new Dictionary<string, string>();
        var splitCookie = cookies.Split(';');

        if (splitCookie.Length < 2)
            return cookies;

        foreach (var cookie in splitCookie)
        {
            var trimmedCookie = cookie.Trim();
            if (string.IsNullOrEmpty(trimmedCookie))
                continue;

            var parts = trimmedCookie.Split('=', 2);
            if (parts.Length == 2)
            {
                cookieDict[parts[0]] = parts[1];
            }
        }

        return string.Join("; ", cookieDict.Select(kv => $"{kv.Key}={kv.Value}"));
    }
}
