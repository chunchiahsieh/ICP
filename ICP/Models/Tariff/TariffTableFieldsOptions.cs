using System.Text.Json.Serialization;

namespace ICP.Models.Tariff;

public class TariffTableFieldsOptions
{
    [JsonPropertyName("tableUi")]
    public TariffTableUiOptions? TableUi { get; set; }

    [JsonPropertyName("initialSort")]
    public TariffTableInitialSort? InitialSort { get; set; }

    [JsonPropertyName("list")]
    public TariffTableFieldListOptions? List { get; set; }
}

public class TariffTableFieldListOptions
{
    [JsonPropertyName("fields")]
    public List<TariffTableFieldEntry> Fields { get; set; } = [];
}

public class TariffTableFieldEntry
{
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("visible")]
    public bool? Visible { get; set; }

    [JsonPropertyName("searchable")]
    public bool? Searchable { get; set; }

    [JsonPropertyName("filterType")]
    public string? FilterType { get; set; }
}

public class TariffTableInitialSort
{
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "asc";
}

public class TariffTableUiOptions
{
    [JsonPropertyName("stickyHeader")]
    public bool? StickyHeader { get; set; }

    [JsonPropertyName("stickyLeftColumns")]
    public bool? StickyLeftColumns { get; set; }

    [JsonPropertyName("maxHeight")]
    public string? MaxHeight { get; set; }

    public static TariffTableUiOptions MergeDefaults(TariffTableUiOptions? source) =>
        new()
        {
            StickyHeader = source?.StickyHeader ?? true,
            StickyLeftColumns = source?.StickyLeftColumns ?? true,
            MaxHeight = string.IsNullOrWhiteSpace(source?.MaxHeight) ? "420px" : source!.MaxHeight
        };
}

public class TariffTableFieldMetadata
{
    public string FieldName { get; init; } = string.Empty;

    public bool Visible { get; init; } = true;

    public bool Searchable { get; init; }

    public string FilterType { get; init; } = "Checkbox";

    public string HeaderLabelKey { get; init; } = string.Empty;
}

public class TariffTablePageConfig
{
    public IReadOnlyList<TariffTableFieldMetadata> Fields { get; init; } = [];

    public TariffTableUiOptions TableUi { get; init; } = TariffTableUiOptions.MergeDefaults(null);

    public TariffTableInitialSort? InitialSort { get; init; }

    public bool HasFilterRow => Fields.Any(field => field.Searchable);

    public int? ResolveInitialSortColumnIndex()
    {
        if (InitialSort is null || string.IsNullOrWhiteSpace(InitialSort.FieldName))
        {
            return null;
        }

        for (var index = 0; index < Fields.Count; index++)
        {
            if (string.Equals(Fields[index].FieldName, InitialSort.FieldName, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return null;
    }
}
