/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Reflection;

namespace FufuLauncher.Helpers
{
    public static class AppVersionHelper
    {
        public const string PreReleaseSuffix = "Pre-release";
        
        public static readonly string FullVersion = ReadFullVersion();
        public static readonly string NumericVersion = StripPreReleaseSuffix(FullVersion);
        public static bool IsPreviewBuild => FullVersion.Contains(PreReleaseSuffix, StringComparison.OrdinalIgnoreCase);
        public static string StripPreReleaseSuffix(string? version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return string.Empty;
            }

            var trimmed = version.Trim();
            if (trimmed.EndsWith(PreReleaseSuffix, StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed.Substring(0, trimmed.Length - PreReleaseSuffix.Length).Trim();
            }
            return trimmed;
        }
        
        public static bool TryParseVersion(string? input, out Version version)
        {
            if (!Version.TryParse(StripPreReleaseSuffix(input), out Version? parsed) || parsed == null)
            {
                version = new Version(0, 0, 0, 0);
                return false;
            }

            version = parsed;
            return true;
        }

        public static bool IsNewerVersion(string candidateVersion, string baselineVersion)
        {
            if (!TryParseVersion(candidateVersion, out var candidateVer) ||
                !TryParseVersion(baselineVersion, out var baselineVer))
            {
                return false;
            }

            return candidateVer > baselineVer;
        }

        private static string ReadFullVersion()
        {
            var numeric = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "1.0.0.0";

            try
            {
                var informational = Assembly.GetEntryAssembly()?
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                    .InformationalVersion;

                if (!string.IsNullOrWhiteSpace(informational))
                {
                    var plusIndex = informational.IndexOf('+');
                    if (plusIndex >= 0)
                    {
                        informational = informational.Substring(0, plusIndex);
                    }

                    var trimmed = informational.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        return trimmed;
                    }
                }
            }
            catch
            {
                // ignored
            }

            return numeric;
        }
    }
}
