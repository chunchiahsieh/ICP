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

    [JsonPropertyName("headerInitialSort")]
    public ShipInfoTableInitialSort? HeaderInitialSort { get; init; }

    [JsonPropertyName("detailInitialSort")]
    public ShipInfoTableInitialSort? DetailInitialSort { get; init; }

    [JsonPropertyName("headerTableUi")]
    public ShipInfoTableUiOptions HeaderTableUi { get; init; } = ShipInfoTableUiOptions.MergeDefaults(null);

    [JsonPropertyName("detailTableUi")]
    public ShipInfoTableUiOptions DetailTableUi { get; init; } = ShipInfoTableUiOptions.MergeDefaults(null);
}
