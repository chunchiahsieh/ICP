using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoFieldMetadata
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("displayNameZh")]
    public string DisplayNameZh { get; set; } = string.Empty;

    [JsonPropertyName("labelKey")]
    public string? LabelKey { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("displayOrder")]
    public int DisplayOrder { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; } = true;

    [JsonPropertyName("searchable")]
    public bool Searchable { get; set; }

    [JsonPropertyName("filterType")]
    public string FilterType { get; set; } = ShipInfoFilterTypes.Checkbox;

    [JsonPropertyName("editable")]
    public bool Editable { get; set; } = true;

    [JsonPropertyName("required")]
    public bool Required { get; set; }

    [JsonPropertyName("controlType")]
    public string ControlType { get; set; } = ShipInfoControlTypes.Text;

    [JsonPropertyName("searchControlType")]
    public string? SearchControlType { get; set; }

    [JsonPropertyName("lookupCategory")]
    public string? LookupCategory { get; set; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("minLength")]
    public int? MinLength { get; set; }

    [JsonPropertyName("maxValue")]
    public decimal? MaxValue { get; set; }

    [JsonPropertyName("minValue")]
    public decimal? MinValue { get; set; }

    [JsonPropertyName("regex")]
    public string? Regex { get; set; }

    [JsonPropertyName("readOnly")]
    public bool ReadOnly { get; set; }

    [JsonPropertyName("permissionCode")]
    public string? PermissionCode { get; set; }

    [JsonPropertyName("tooltip")]
    public string? Tooltip { get; set; }

    [JsonPropertyName("defaultValue")]
    public string? DefaultValue { get; set; }

    [JsonPropertyName("group")]
    public string? Group { get; set; }

    [JsonPropertyName("tab")]
    public string? Tab { get; set; }

    [JsonPropertyName("section")]
    public string? Section { get; set; }
}
