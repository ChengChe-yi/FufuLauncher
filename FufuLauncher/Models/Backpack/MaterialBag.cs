/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.Backpack;

public sealed record MaterialEntry(
    [property: JsonPropertyName("id")]       uint   Id,
    [property: JsonPropertyName("name")]     string Name,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("count")]    ulong  Count
);

public sealed record MaterialBag(
    [property: JsonPropertyName("materials")] MaterialEntry[] Materials
);
