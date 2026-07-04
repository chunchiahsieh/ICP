using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoTableFieldsOptions
{
    [JsonPropertyName("header")]
    public ShipInfoTableSectionOptions Header { get; set; } = new();

    [JsonPropertyName("detail")]
    public ShipInfoTableSectionOptions Detail { get; set; } = new();
}

public class ShipInfoTableSectionOptions
{
    [JsonPropertyName("tableUi")]
    public ShipInfoTableUiOptions? TableUi { get; set; }

    [JsonPropertyName("initialSort")]
    public ShipInfoTableInitialSort? InitialSort { get; set; }

    [JsonPropertyName("fields")]
    public List<ShipInfoTableFieldEntry> Fields { get; set; } = [];
}

public class ShipInfoTableFieldEntry
{
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("visible")]
    public bool? Visible { get; set; }

    [JsonPropertyName("searchable")]
    public bool? Searchable { get; set; }

    [JsonPropertyName("filterType")]
    public string? FilterType { get; set; }

    [JsonPropertyName("labelKey")]
    public string? LabelKey { get; set; }
}

public class ShipInfoTableInitialSort
{
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "asc";
}
