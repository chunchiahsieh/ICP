using ICP.Helpers;
using ICP.Models.ShipInfo;
using ICP.Models.Tariff;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class TariffTableMetadataProvider
{
    private static readonly IReadOnlyDictionary<string, string> DefaultHeaderLabelKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RowNo"] = "Broker.TariffData.Column.RowNo",
            ["DeclarationPdf"] = "Broker.TariffData.Column.PDF",
            ["CostFile"] = "Broker.TariffData.Column.Cost",
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

        return new TariffTablePageConfig
        {
            Fields = fields,
            TableUi = TariffTableUiOptions.MergeDefaults(options.TableUi),
            InitialSort = options.InitialSort
        };
    }

    public bool IsCheckboxFilterColumn(string column)
    {
        var config = GetPageConfig();
        var field = config.Fields.FirstOrDefault(item =>
            string.Equals(item.FieldName, column, StringComparison.OrdinalIgnoreCase));
        return field is not null && field.Searchable && TariffMetadataHelper.IsCheckboxFilter(field);
    }

    private static TariffTableFieldMetadata ResolveFieldMetadata(TariffTableFieldEntry entry)
    {
        var fieldName = entry.FieldName?.Trim() ?? string.Empty;
        return new TariffTableFieldMetadata
        {
            FieldName = fieldName,
            Visible = entry.Visible ?? true,
            Searchable = entry.Searchable ?? false,
            FilterType = ShipInfoFilterTypes.Normalize(entry.FilterType),
            HeaderLabelKey = DefaultHeaderLabelKeys.TryGetValue(fieldName, out var key)
                ? key
                : $"Broker.TariffData.Column.{fieldName}"
        };
    }

    private static List<TariffTableFieldMetadata> BuildDefaultFields() =>
        DefaultHeaderLabelKeys
            .Select(pair => new TariffTableFieldMetadata
            {
                FieldName = pair.Key,
                Visible = true,
                Searchable = !TariffMetadataHelper.IsVirtualField(pair.Key),
                FilterType = "Checkbox",
                HeaderLabelKey = pair.Value
            })
            .ToList();
}
