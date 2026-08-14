/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

namespace FufuLauncher.Services.MiHoYo.Passport;

public static class PassportDeviceId
{
    public static string Generate53()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
        return string.Create(53, chars, static (span, state) =>
        {
            for (int i = 0; i < span.Length; i++)
            {
                span[i] = state[Random.Shared.Next(state.Length)];
            }
        });
    }
}
