/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json;
using FufuLauncher.Models.MiHoYo.Passport;
using FufuLauncher.Views;
using Microsoft.UI.Xaml;

namespace FufuLauncher.Services.MiHoYo.Passport;

public sealed class OverseaRiskVerificationService
{
    public async Task<bool> TryVerifyAsync(IVerifyProvider provider, string? rawRisk, XamlRoot? xamlRoot, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(rawRisk) || xamlRoot is null)
        {
            return false;
        }

        Risk? risk = Deserialize<Risk>(rawRisk);
        if (risk is null || string.IsNullOrEmpty(risk.VerifyString))
        {
            return false;
        }

        RiskVerify? riskVerify = Deserialize<RiskVerify>(risk.VerifyString);
        if (riskVerify is null)
        {
            return false;
        }

        var dialog = new OverseaEmailVerificationDialog { XamlRoot = xamlRoot };
        if (await dialog.TryValidateAsync(riskVerify.Ticket, token).ConfigureAwait(false))
        {
            risk.VerifyString = null;

            provider.Verify = JsonSerializer.Serialize(risk);
            return true;
        }

        return false;
    }

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
