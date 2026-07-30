/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.Backpack;

public sealed record WeaponEntry(
    [property: JsonPropertyName("id")]          uint   Id,
    [property: JsonPropertyName("guid")]        string Guid,
    [property: JsonPropertyName("name")]        string Name,
    [property: JsonPropertyName("type")]        string Type,
    [property: JsonPropertyName("rank")]        int    Rank,
    [property: JsonPropertyName("specialProp")] string SpecialProp,
    [property: JsonPropertyName("level")]       int    Level,
    [property: JsonPropertyName("promote")]     int    Promote,
    [property: JsonPropertyName("refine")]      int    Refine
);

public sealed record WeaponBag(
    [property: JsonPropertyName("weapons")] WeaponEntry[] Weapons
);
