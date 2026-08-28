using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

/// <summary>
/// Ship Info Header form UI metadata. This model intentionally controls only rendering;
/// SaveHeaderAsync remains the authoritative update boundary.
/// </summary>
public sealed class ShipInfoFormMetadata
{
    [JsonPropertyName("formId")]
    public string FormId { get; set; } = string.Empty;

    [JsonPropertyName("metadataVersion")]
    public string MetadataVersion { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public Dictionary<string, ShipInfoFormFieldDefinition> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("modes")]
    public Dictionary<string, ShipInfoFormModeDefinition> Modes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ShipInfoFormFieldDefinition
{
    [JsonPropertyName("labelKey")]
    public string? LabelKey { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("maxLength")]
    public int? MaxLength { get; set; }

    [JsonPropertyName("placeholderKey")]
    public string? PlaceholderKey { get; set; }

    [JsonPropertyName("placeholder")]
    public string? Placeholder { get; set; }

    [JsonPropertyName("helpTextKey")]
    public string? HelpTextKey { get; set; }

    [JsonPropertyName("helpText")]
    public string? HelpText { get; set; }

    [JsonPropertyName("options")]
    public List<ShipInfoFormSelectOption>? Options { get; set; }

    [JsonPropertyName("optionsSource")]
    public string? OptionsSource { get; set; }

    [JsonPropertyName("lookupCategory")]
    public string? LookupCategory { get; set; }

    [JsonPropertyName("checkedValue")]
    public string? CheckedValue { get; set; }

    [JsonPropertyName("uncheckedValue")]
    public string? UncheckedValue { get; set; }
}

public sealed class ShipInfoFormSelectOption
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("labelKey")]
    public string? LabelKey { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public sealed class ShipInfoFormModeDefinition
{
    [JsonPropertyName("groups")]
    public List<ShipInfoFormGroupDefinition> Groups { get; set; } = [];
}

public sealed class ShipInfoFormGroupDefinition
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("labelKey")]
    public string? LabelKey { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [JsonPropertyName("order")]
    public int? Order { get; set; }

    [JsonPropertyName("columns")]
    public int? Columns { get; set; }

    [JsonPropertyName("fields")]
    public List<ShipInfoFormModeFieldDefinition> Fields { get; set; } = [];
}

public sealed class ShipInfoFormModeFieldDefinition
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public int? Order { get; set; }

    [JsonPropertyName("readOnly")]
    public bool? ReadOnly { get; set; }

    [JsonPropertyName("required")]
    public bool? Required { get; set; }

    [JsonPropertyName("columnSpan")]
    public int? ColumnSpan { get; set; }
}
