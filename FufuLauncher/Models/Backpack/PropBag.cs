using System.Text.Json.Serialization;

namespace FufuLauncher.Models.Backpack;

public sealed record PropBag(
    [property: JsonPropertyName("props")] IReadOnlyDictionary<uint, long> Props
);
