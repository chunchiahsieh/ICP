using ICP.Models.Tariff;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class TariffTableMetadataProvider
{
    private static readonly IReadOnlyDictionary<string, string> DefaultHeaderLabelKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RowNo"] = "Broker.TariffData.Column.RowNo",
            ["MAWB"] = "Broker.TariffData.Column.MAWB",
            ["HAWB"] = "Broker.TariffData.Column.HAWB",
            ["ImportDate"] = "Broker.TariffData.Column.ImportDate",
            ["DeclarationDate"] = "Broker.TariffData.Column.DeclarationDate",
            ["ReleaseDate"] = "Broker.TariffData.Column.ReleaseDate",
            ["InvoiceNumber"] = "Broker.TariffData.Column.InvoiceNumber",
            ["DescriptionOfGoods"] = "Broker.TariffData.Column.DescriptionOfGoods",
            ["HTSNumber"] = "Broker.TariffData.Column.HTSNumber",
            ["EntryNumber"] = "Broker.TariffData.Column.EntryNumber",
            ["Mode"] = "Broker.TariffData.Column.Mode",
            ["PortOfDeparture"] = "Broker.TariffData.Column.PortOfDeparture",
            ["FlightNo"] = "Broker.TariffData.Column.FlightNo",
            ["Shipper"] = "Broker.TariffData.Column.Shipper",
            ["Broker"] = "Broker.TariffData.Column.Broker",
            ["AirSea"] = "Broker.TariffData.Column.AirSea",
            ["CreateDate"] = "Broker.TariffData.Column.CreateDate"
        };

    private static readonly IReadOnlyDictionary<string, string> FilterQueryParamMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DescriptionOfGoods"] = "DescriptionOfGoodsList"
        };

    private readonly IOptionsMonitor<TariffTableFieldsOptions> _tableFieldsOptions;
    private readonly ILogger<TariffTableMetadataProvider> _logger;

    public TariffTableMetadataProvider(
        IOptionsMonitor<TariffTableFieldsOptions> tableFieldsOptions,
        ILogger<TariffTableMetadataProvider> logger)
    {
        _tableFieldsOptions = tableFieldsOptions;
        _logger = logger;
    }

    public TariffTablePageConfig GetPageConfig()
    {
        var options = _tableFieldsOptions.CurrentValue;
        var entries = options.List?.Fields ?? [];
        var fields = entries
            .Select(ResolveFieldMetadata)
            .Where(field => field.Visible)
            .ToList();

        if (fields.Count == 0)
        {
            _logger.LogWarning("tariff-table-fields.json has no visible fields; using built-in defaults.");
            fields = BuildDefaultFields();
        }

        var filterFieldMap = fields
            .Where(field => field.Searchable)
            .ToDictionary(
                field => $"filter-{field.FieldName}",
                field => ResolveFilterQueryParam(field.FieldName),
                StringComparer.OrdinalIgnoreCase);

        return new TariffTablePageConfig
        {
            Fields = fields,
            TableUi = TariffTableUiOptions.MergeDefaults(options.TableUi),
            InitialSort = options.InitialSort,
            FilterFieldMap = filterFieldMap
        };
    }

    public bool IsSearchableColumn(string column)
    {
        var config = GetPageConfig();
        return config.Fields.Any(field =>
            field.Searchable
            && string.Equals(field.FieldName, column, StringComparison.OrdinalIgnoreCase));
    }

    private static TariffTableFieldMetadata ResolveFieldMetadata(TariffTableFieldEntry entry)
    {
        var fieldName = entry.FieldName?.Trim() ?? string.Empty;
        return new TariffTableFieldMetadata
        {
            FieldName = fieldName,
            Visible = entry.Visible ?? true,
            Searchable = entry.Searchable ?? false,
            FilterType = string.IsNullOrWhiteSpace(entry.FilterType) ? "Checkbox" : entry.FilterType,
            HeaderLabelKey = DefaultHeaderLabelKeys.TryGetValue(fieldName, out var key)
                ? key
                : $"Broker.TariffData.Column.{fieldName}"
        };
    }

    public static string ResolveFilterQueryParam(string fieldName)
    {
        if (FilterQueryParamMap.TryGetValue(fieldName, out var paramName))
        {
            return paramName;
        }

        return fieldName + "s";
    }

    private static List<TariffTableFieldMetadata> BuildDefaultFields() =>
        DefaultHeaderLabelKeys
            .Select(pair => new TariffTableFieldMetadata
            {
                FieldName = pair.Key,
                Visible = true,
                Searchable = !string.Equals(pair.Key, "RowNo", StringComparison.OrdinalIgnoreCase),
                FilterType = "Checkbox",
                HeaderLabelKey = pair.Value
            })
            .ToList();
}
