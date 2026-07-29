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

    /// <summary>向後相容：等同 list.fields。</summary>
    [JsonPropertyName("fields")]
    public List<ShipInfoTableFieldEntry>? Fields { get; set; }

    [JsonPropertyName("list")]
    public ShipInfoTableFieldListOptions? List { get; set; }

    [JsonPropertyName("edit")]
    public ShipInfoTableFieldListOptions? Edit { get; set; }

    public IReadOnlyList<ShipInfoTableFieldEntry> ResolveListFieldEntries() =>
        List?.Fields is { Count: > 0 } listFields
            ? listFields
            : Fields ?? [];

    public IReadOnlyList<ShipInfoTableFieldEntry> ResolveEditFieldEntries() =>
        Edit?.Fields ?? [];
}

public class ShipInfoTableFieldListOptions
{
    /// <summary>為 true 時，edit 包含 catalog 全部欄位（排除系統稽核欄位），fields 僅作覆寫。</summary>
    [JsonPropertyName("includeAllExceptSystem")]
    public bool IncludeAllExceptSystem { get; set; }

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

    [JsonPropertyName("editable")]
    public bool? Editable { get; set; }

    [JsonPropertyName("controlType")]
    public string? ControlType { get; set; }

    [JsonPropertyName("lookupCategory")]
    public string? LookupCategory { get; set; }

    [JsonPropertyName("required")]
    public bool? Required { get; set; }

    [JsonPropertyName("readOnly")]
    public bool? ReadOnly { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }
}

public class ShipInfoTableInitialSort
{
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "asc";
}
