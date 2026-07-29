using FufuLauncher.Helpers;

namespace FufuLauncher.Helpers;

internal static class BackpackLocalization
{
    public static string Get(string key) => $"Backpack_{key}".GetLocalized();
}
