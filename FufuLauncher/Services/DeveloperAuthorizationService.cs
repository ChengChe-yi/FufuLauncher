/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text;
using System.Text.Json;
using FufuLauncher.Helpers;

namespace FufuLauncher.Services;

public sealed class DeveloperAuthorizationService
{
    private const string VerifyHwidUrl = "https://dev.s1ky3.xyz/api/verify-hwid";

    private bool _hasChecked;
    private bool _isAuthorized;
    
    public async Task<bool> IsAuthorizedAsync()
    {
        if (_hasChecked && _isAuthorized)
        {
            return true;
        }

        string hwid = await Task.Run(SystemEnvironmentHelper.GetHwid).ConfigureAwait(false);

        bool authorized = false;
        if (!string.IsNullOrEmpty(hwid) && !string.Equals(hwid, "Unknown", StringComparison.Ordinal))
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                var content = new StringContent(JsonSerializer.Serialize(new { hwid }), Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(VerifyHwidUrl, content).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    string responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    using var result = JsonDocument.Parse(responseString);
                    if (result.RootElement.TryGetProperty("authorized", out var authElement) && authElement.GetBoolean())
                    {
                        authorized = true;
                    }
                }
            }
            catch
            {
                // ignored
            }
        }

        _hasChecked = true;
        _isAuthorized = authorized;
        return authorized;
    }
}
