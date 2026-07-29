using System.Text.Json.Serialization;

namespace FufuLauncher.Models.Backpack;

public sealed record ArtifactMainStat(
    [property: JsonPropertyName("type")]    string Type,
    [property: JsonPropertyName("typeRaw")] string TypeRaw
);

public sealed record ArtifactSubStat(
    [property: JsonPropertyName("type")]    string   Type,
    [property: JsonPropertyName("typeRaw")] string   TypeRaw,
    [property: JsonPropertyName("value")]   double   Value,
    [property: JsonPropertyName("rolls")]   double[] Rolls
);

public sealed record ArtifactEntry(
    [property: JsonPropertyName("id")]       uint              Id,
    [property: JsonPropertyName("guid")]     string            Guid,
    [property: JsonPropertyName("setName")]  string            SetName,
    [property: JsonPropertyName("name")]     string            Name,
    [property: JsonPropertyName("slot")]     string            Slot,
    [property: JsonPropertyName("locked")]    bool              Locked,
    [property: JsonPropertyName("level")]    int               Level,
    [property: JsonPropertyName("rank")]     int               Rank,
    [property: JsonPropertyName("mainStat")] ArtifactMainStat  MainStat,
    [property: JsonPropertyName("subStats")] ArtifactSubStat[] SubStats
);

public sealed record ArtifactBag(
    [property: JsonPropertyName("artifacts")] ArtifactEntry[] Artifacts
);
