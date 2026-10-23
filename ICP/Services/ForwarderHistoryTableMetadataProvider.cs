using ICP.Helpers;
using ICP.Models.Forwarder;
using ICP.Models.Tariff;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace ICP.Services;

public sealed class ForwarderHistoryTableMetadataProvider(IOptionsMonitor<ForwarderTableFieldsOptions> options)
{
    public const string OptionsName = "ForwarderUploadHistory";
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["FileName"] = "FileName", ["ImportedTime"] = "ImportedTime", ["ImportedBy"] = "ImportedBy",
        ["RowCount"] = "RowCount", ["Status"] = "Status", ["DownloadOriginal"] = "DownloadOriginal"
    };

    public ForwarderTablePageConfig GetConfig()
    {
        var config = options.Get(OptionsName);
        var entries = config.List?.Fields ?? [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var fields = entries.Where(entry => Labels.ContainsKey(entry.FieldName) && seen.Add(entry.FieldName))
            .Select(entry => new ForwarderTableFieldMetadata
            {
                FieldName = entry.FieldName, Visible = entry.Visible ?? true, Searchable = entry.Searchable ?? false,
                FilterType = ICP.Models.ShipInfo.ShipInfoFilterTypes.Normalize(entry.FilterType),
                HeaderLabelKey = "Forwarder.Upload." + Labels[entry.FieldName], HeaderLabel = entry.FieldName
            }).ToList();
        return new ForwarderTablePageConfig { Fields = fields, InitialSort = config.InitialSort, TableUi = ForwarderTableUiOptions.MergeDefaults(config.TableUi) };
    }

    public static string Value(ForwarderUploadHistoryRow row, string field) => field switch
    {
        "FileName" => row.FileName, "ImportedTime" => row.ImportedTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
        "ImportedBy" => row.ImportedBy, "RowCount" => row.RowCount.ToString(CultureInfo.InvariantCulture),
        "Status" => "Imported", "DownloadOriginal" => row.FileExists ? "Has" : "None", _ => ""
    };

    public static List<ForwarderUploadHistoryRow> Filter(IEnumerable<ForwarderUploadHistoryRow> rows, TariffDataQueryModel criteria,
        ForwarderTablePageConfig config)
    {
        foreach (var field in config.Fields.Where(field => field.Searchable))
        {
            if (field.FilterType == "Text" && criteria.Text.TryGetValue(field.FieldName, out var text) && !string.IsNullOrWhiteSpace(text))
                rows = rows.Where(row => Value(row, field.FieldName).Contains(text.Trim(), StringComparison.OrdinalIgnoreCase));
            if (field.FilterType == "Checkbox" && criteria.Checkbox.TryGetValue(field.FieldName, out var selected) && selected.Count > 0)
                rows = rows.Where(row => selected.Any(value => value == "__BLANK__" ? string.IsNullOrWhiteSpace(Value(row, field.FieldName))
                    : string.Equals(value, Value(row, field.FieldName), StringComparison.OrdinalIgnoreCase)));
            if (field.FilterType == "DateRange" && field.FieldName == "ImportedTime")
            {
                if (criteria.DateFrom.TryGetValue(field.FieldName, out var from) && DateOnly.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fromDate))
                    rows = rows.Where(row => DateOnly.FromDateTime(row.ImportedTime) >= fromDate);
                if (criteria.DateTo.TryGetValue(field.FieldName, out var to) && DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var toDate))
                    rows = rows.Where(row => DateOnly.FromDateTime(row.ImportedTime) <= toDate);
            }
            if (field.FilterType == "Date" && field.FieldName == "ImportedTime"
                && criteria.Date.TryGetValue(field.FieldName, out var date)
                && DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var exactDate))
                rows = rows.Where(row => DateOnly.FromDateTime(row.ImportedTime) == exactDate);
        }
        var sort = config.InitialSort;
        if (sort is not null)
        {
            Func<ForwarderUploadHistoryRow, IComparable> key = sort.FieldName switch
            {
                "ImportedTime" => row => row.ImportedTime, "RowCount" => row => row.RowCount,
                _ => row => Value(row, sort.FieldName)
            };
            rows = sort.Direction.Equals("asc", StringComparison.OrdinalIgnoreCase) ? rows.OrderBy(key) : rows.OrderByDescending(key);
        }
        return rows.ToList();
    }
}

public sealed class ForwarderHistoryListViewModel
{
    public ForwarderTablePageConfig Config { get; init; } = new();
    public List<ForwarderUploadHistoryRow> Items { get; init; } = [];
}
