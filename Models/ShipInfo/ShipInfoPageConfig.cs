using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoPageConfig
{
    [JsonPropertyName("culture")]
    public string Culture { get; init; } = "zh-TW";

    [JsonPropertyName("headerFields")]
    public IReadOnlyList<ShipInfoFieldMetadata> HeaderFields { get; init; } = [];

    [JsonPropertyName("detailFields")]
    public IReadOnlyList<ShipInfoFieldMetadata> DetailFields { get; init; } = [];

    [JsonPropertyName("searchFields")]
    public IReadOnlyList<ShipInfoFieldMetadata> SearchFields { get; init; } = [];

    [JsonPropertyName("statusRules")]
    public IReadOnlyDictionary<string, ShipInfoActionPermission> StatusRules { get; init; }
        = new Dictionary<string, ShipInfoActionPermission>();
}
