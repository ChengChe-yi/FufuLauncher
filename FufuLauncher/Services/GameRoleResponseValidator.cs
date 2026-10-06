/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/

using System.Text.Json;

namespace FufuLauncher.Services;

public static class GameRoleResponseValidator
{
    public static bool MatchesWidget(string json, string uid, string region)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("data", out var data)) return false;
        if (data.TryGetProperty("role", out var role)) data = role;
        else if (data.TryGetProperty("game_role", out role)) data = role;
        var returnedUid = Read(data, "game_uid") ?? Read(data, "game_role_id") ?? Read(data, "uid");
        var returnedRegion = Read(data, "region") ?? Read(data, "server");
        return returnedUid == uid && returnedRegion == region;
    }

    private static string? Read(JsonElement data, string key) =>
        data.ValueKind == JsonValueKind.Object && data.TryGetProperty(key, out var value)
            ? value.ToString()
            : null;
}