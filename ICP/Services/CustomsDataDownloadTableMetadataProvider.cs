using ICP.Helpers;
using ICP.Models.CustomsDataDownload;
using ICP.Models.ShipInfo;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class CustomsDataDownloadTableMetadataProvider
{
    private static readonly string[] DefaultFieldOrder =
    [
        "RunId", "FileCode", "SourceFileName", "SourceFileDate", "CreatedUtc",
        "InvoiceNo", "Forwarder", "Etd", "Eta", "InvoiceDate", "Mawb", "Hawb", "Flt",
        "DestinationPort", "DestinationCountry", "InvoiceType", "Incoterms", "Bu",
        "PoNo", "PoLine", "OrderPriority", "InvoiceSeq", "ItemNo", "Description",
        "Qty", "Uom", "Coo", "Price", "Amount", "Currency", "PackingType", "CartonNo",
        "Length", "Width", "Hight", "GrossWeight", "TotalCartons", "NetWeightOfTheItem",
        "NcdrNo", "EndUserCode", "EndUser", "MachineNo", "MachineType", "ShipReason",
        "DeliveryNo", "DeliveryLineNo", "SoldToPartyCode", "SoldToParty",
        "ShipToPartyCode", "ShipToParty", "ShipToPartyAddress", "Hazmat", "WbsElement",
        "SapRemarks"
    ];

    private readonly IOptionsMonitor<CustomsDataDownloadTableFieldsOptions> _tableFieldsOptions;
    private readonly ILogger<CustomsDataDownloadTableMetadataProvider> _logger;

    public CustomsDataDownloadTableMetadataProvider(
        IOptionsMonitor<CustomsDataDownloadTableFieldsOptions> tableFieldsOptions,
        ILogger<CustomsDataDownloadTableMetadataProvider> logger)
    {
        _tableFieldsOptions = tableFieldsOptions;
        _logger = logger;
    }

    public CustomsDataDownloadTablePageConfig GetPageConfig()
    {
        var options = _tableFieldsOptions.CurrentValue;
        var entries = options.List?.Fields ?? [];
        var fields = entries
            .Select(ResolveFieldMetadata)
            .Where(field => field.Visible)
            .ToList();

        if (fields.Count == 0)
        {
            _logger.LogWarning("customs-data-download-table-fields.json has no visible fields; using built-in defaults.");
            fields = BuildDefaultFields();
        }

        return new CustomsDataDownloadTablePageConfig
        {
            Fields = fields,
            TableUi = CustomsDataDownloadTableUiOptions.MergeDefaults(options.TableUi),
            InitialSort = options.InitialSort
        };
    }

    public bool IsCheckboxFilterColumn(string column)
    {
        var config = GetPageConfig();
        var field = config.Fields.FirstOrDefault(item =>
            string.Equals(item.FieldName, column, StringComparison.OrdinalIgnoreCase));
        return field is not null && field.Searchable && CustomsDataDownloadMetadataHelper.IsCheckboxFilter(field);
    }

    private static CustomsDataDownloadTableFieldMetadata ResolveFieldMetadata(CustomsDataDownloadTableFieldEntry entry)
    {
        var fieldName = entry.FieldName?.Trim() ?? string.Empty;
        return new CustomsDataDownloadTableFieldMetadata
        {
            FieldName = fieldName,
            Visible = entry.Visible ?? true,
            Searchable = entry.Searchable ?? false,
            FilterType = ShipInfoFilterTypes.Normalize(entry.FilterType),
            HeaderLabelKey = $"Broker.CustomsDataDownload.Column.{fieldName}"
        };
    }

    private static List<CustomsDataDownloadTableFieldMetadata> BuildDefaultFields() =>
        DefaultFieldOrder
            .Select(fieldName => new CustomsDataDownloadTableFieldMetadata
            {
                FieldName = fieldName,
                Visible = true,
                Searchable = true,
                FilterType = "Checkbox",
                HeaderLabelKey = $"Broker.CustomsDataDownload.Column.{fieldName}"
            })
            .ToList();
}
