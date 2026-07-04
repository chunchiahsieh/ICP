using System.Text.Json.Serialization;

namespace ICP.Models.Forwarder;

public class ForwarderTableFieldsOptions
{
    [JsonPropertyName("tableUi")]
    public ForwarderTableUiOptions? TableUi { get; set; }

    [JsonPropertyName("initialSort")]
    public ForwarderTableInitialSort? InitialSort { get; set; }

    [JsonPropertyName("list")]
    public ForwarderTableFieldListOptions? List { get; set; }
}

public class ForwarderTableFieldListOptions
{
    [JsonPropertyName("fields")]
    public List<ForwarderTableFieldEntry> Fields { get; set; } = [];
}

public class ForwarderTableFieldEntry
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

public class ForwarderTableInitialSort
{
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "asc";
}

public class ForwarderTableUiOptions
{
    [JsonPropertyName("stickyHeader")]
    public bool? StickyHeader { get; set; }

    [JsonPropertyName("stickyLeftColumns")]
    public bool? StickyLeftColumns { get; set; }

    [JsonPropertyName("maxHeight")]
    public string? MaxHeight { get; set; }

    public static ForwarderTableUiOptions MergeDefaults(ForwarderTableUiOptions? source) =>
        new()
        {
            StickyHeader = source?.StickyHeader ?? true,
            StickyLeftColumns = source?.StickyLeftColumns ?? true,
            MaxHeight = string.IsNullOrWhiteSpace(source?.MaxHeight) ? "500px" : source!.MaxHeight
        };
}

public class ForwarderTableFieldMetadata
{
    public string FieldName { get; init; } = string.Empty;

    public bool Visible { get; init; } = true;

    public bool Searchable { get; init; }

    public string FilterType { get; init; } = "Checkbox";

    public string HeaderLabel { get; init; } = string.Empty;
}

public class ForwarderTablePageConfig
{
    public IReadOnlyList<ForwarderTableFieldMetadata> Fields { get; init; } = [];

    public ForwarderTableUiOptions TableUi { get; init; } = ForwarderTableUiOptions.MergeDefaults(null);

    public ForwarderTableInitialSort? InitialSort { get; init; }

    public bool HasFilterRow => Fields.Any(field => field.Searchable);

    public IReadOnlyDictionary<string, string> FilterFieldMap { get; init; }
        = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
