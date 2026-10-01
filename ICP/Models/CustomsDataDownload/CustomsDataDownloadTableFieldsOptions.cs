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

    [JsonPropertyName("thenBy")]
    public List<CustomsDataDownloadTableInitialSort> ThenBy { get; set; } = [];
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
        var columns = ResolveInitialSortColumns();
        return columns.Count == 0 ? null : (int)columns[0][0];
    }

    public IReadOnlyList<object[]> ResolveInitialSortColumns()
    {
        if (InitialSort is null) return [];

        var visible = Fields.Where(field => field.Visible).ToList();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columns = new List<object[]>();
        foreach (var sort in new[] { InitialSort }.Concat(InitialSort.ThenBy ?? []))
        {
            var name = sort?.FieldName?.Trim();
            if (string.IsNullOrEmpty(name) || !seen.Add(name)) continue;

            var index = visible.FindIndex(field =>
                string.Equals(field.FieldName, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0) continue;

            columns.Add([index,
                string.Equals(sort!.Direction?.Trim(), "desc", StringComparison.OrdinalIgnoreCase)
                    ? "desc" : "asc"]);
        }

        return columns;
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
