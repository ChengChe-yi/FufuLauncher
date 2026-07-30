/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Text.Json.Serialization;

namespace FufuLauncher.Models.Backpack;

public sealed record PropBag(
    [property: JsonPropertyName("props")] IReadOnlyDictionary<uint, long> Props
);
