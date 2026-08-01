/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using FufuLauncher.ViewModels;

namespace FufuLauncher.Helpers;

public static class LanguagePreferenceResolver
{
    public const string FallbackCulture = "zh-CN";

    private static readonly IReadOnlyDictionary<AppLanguage, string> ExplicitCultures =
        new Dictionary<AppLanguage, string>
        {
            [AppLanguage.zhCN] = "zh-CN", [AppLanguage.zhTW] = "zh-TW",
            [AppLanguage.enUS] = "en-US", [AppLanguage.fr] = "fr-FR",
            [AppLanguage.de] = "de-DE", [AppLanguage.ru] = "ru-RU",
            [AppLanguage.ja] = "ja-JP", [AppLanguage.es] = "es-ES",
            [AppLanguage.esMX] = "es-MX", [AppLanguage.ko] = "ko-KR",
            [AppLanguage.it] = "it-IT", [AppLanguage.id] = "id-ID",
            [AppLanguage.pt] = "pt-BR"
        };

    private static readonly string[] SupportedCultures =
        ["zh-CN", "zh-TW", "en-US", "fr-FR", "de-DE", "ru-RU", "ja-JP",
         "es-ES", "es-MX", "ko-KR", "it-IT", "id-ID", "pt-BR"];

    public static string Resolve(AppLanguage language, IEnumerable<string>? systemLanguagePreferences)
    {
        return ExplicitCultures.TryGetValue(language, out var culture)
            ? culture
            : ResolveSystemCulture(systemLanguagePreferences);
    }

    public static string ResolveSystemCulture(IEnumerable<string>? systemLanguagePreferences)
    {
        if (systemLanguagePreferences == null)
            return FallbackCulture;

        foreach (var preferredCulture in systemLanguagePreferences)
        {
            if (string.IsNullOrWhiteSpace(preferredCulture))
                continue;

            var exactMatch = SupportedCultures.FirstOrDefault(culture =>
                string.Equals(culture, preferredCulture, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;

            var languageCode = preferredCulture.Trim().Split(['-', '_'], 2)[0];
            var languageMatch = SupportedCultures.FirstOrDefault(culture =>
                culture.StartsWith($"{languageCode}-", StringComparison.OrdinalIgnoreCase));
            if (languageMatch != null)
                return languageMatch;
        }

        return FallbackCulture;
    }
}
