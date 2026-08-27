using System.Text.Json.Serialization;

namespace ICP.Models.CustomsDataDownload;

public class CustomsDataDownloadTableFieldsOptions
{
    [JsonPropertyName("tableUi")]
    public CustomsDataDownloadTableUiOptions? TableUi { get; set; }

    [JsonPropertyName("initialSort")]
    public CustomsDataDownloadTableInitialSort? InitialSort { get; set; }

    [JsonPropertyName("list")]
    public CustomsDataDownloadTableFieldListOptions? List { get; set; }
}

public class CustomsDataDownloadTableFieldListOptions
{
    [JsonPropertyName("fields")]
    public List<CustomsDataDownloadTableFieldEntry> Fields { get; set; } = [];
}

public class CustomsDataDownloadTableFieldEntry
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

public class CustomsDataDownloadTableInitialSort
{
    [JsonPropertyName("fieldName")]
    public string FieldName { get; set; } = string.Empty;

    [JsonPropertyName("direction")]
    public string Direction { get; set; } = "asc";
}

public class CustomsDataDownloadTableUiOptions
{
    [JsonPropertyName("stickyHeader")]
    public bool? StickyHeader { get; set; }

    [JsonPropertyName("stickyLeftColumns")]
    public bool? StickyLeftColumns { get; set; }

    [JsonPropertyName("maxHeight")]
    public string? MaxHeight { get; set; }

    public static CustomsDataDownloadTableUiOptions MergeDefaults(CustomsDataDownloadTableUiOptions? source) =>
        new()
        {
            StickyHeader = source?.StickyHeader ?? true,
            StickyLeftColumns = source?.StickyLeftColumns ?? true,
            MaxHeight = string.IsNullOrWhiteSpace(source?.MaxHeight) ? "600px" : source!.MaxHeight
        };
}

public class CustomsDataDownloadTableFieldMetadata
{
    public string FieldName { get; init; } = string.Empty;

    public bool Visible { get; init; } = true;

    public bool Searchable { get; init; }

    public string FilterType { get; init; } = "Checkbox";

    public string HeaderLabelKey { get; init; } = string.Empty;
}

public class CustomsDataDownloadTablePageConfig
{
    public IReadOnlyList<CustomsDataDownloadTableFieldMetadata> Fields { get; init; } = [];

    public CustomsDataDownloadTableUiOptions TableUi { get; init; } =
        CustomsDataDownloadTableUiOptions.MergeDefaults(null);

    public CustomsDataDownloadTableInitialSort? InitialSort { get; init; }

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

public class CustomsDataDownloadQueryModel
{
    public Dictionary<string, List<string>> Checkbox { get; set; } = [];

    public Dictionary<string, string> Text { get; set; } = [];

    public Dictionary<string, string> DateFrom { get; set; } = [];

    public Dictionary<string, string> DateTo { get; set; } = [];

    public Dictionary<string, string> Date { get; set; } = [];
}
