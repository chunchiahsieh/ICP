using ICP.Models.Forwarder;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public class ForwarderTableMetadataProvider
{
    private static readonly IReadOnlyDictionary<string, string> DefaultHeaderLabels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DuplicateStatus"] = "Duplicate",
            ["RowNo"] = "No.",
            ["Type"] = "Type",
            ["InvoiceNo"] = "Invoice No.",
            ["CustomerReference"] = "Customer Reference",
            ["MaterialCode"] = "Material Code",
            ["OrderMaterialName"] = "Order Material Name",
            ["Quantity"] = "Quantity",
            ["PortOfLoading"] = "Port of Loading",
            ["ShipToName"] = "Ship to Name",
            ["ShipToAddress"] = "Ship to Address",
            ["ShipToPartyCountryCode"] = "Ship to Party Country Code",
            ["ShipToPortCode"] = "Ship to Port Code",
            ["FreightCharge"] = "Freight charge",
            ["ConfirmedCustomDate"] = "Confirmed Custom Date",
            ["Hawb"] = "HAWB",
            ["Mawb"] = "MAWB",
            ["Etd"] = "ETD",
            ["Eta"] = "ETA",
            ["Flight1"] = "FLIGHT 1#",
            ["Flight2"] = "FLIGHT 2#",
            ["Cb"] = "C/B",
            ["Action"] = "Action",
            ["Mdp"] = "MDP"
        };

    private static readonly IReadOnlyDictionary<string, string> FilterQueryParamMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DuplicateStatus"] = "DuplicateStatuses"
        };

    private readonly IOptionsMonitor<ForwarderTableFieldsOptions> _tableFieldsOptions;
    private readonly ILogger<ForwarderTableMetadataProvider> _logger;

    public ForwarderTableMetadataProvider(
        IOptionsMonitor<ForwarderTableFieldsOptions> tableFieldsOptions,
        ILogger<ForwarderTableMetadataProvider> logger)
    {
        _tableFieldsOptions = tableFieldsOptions;
        _logger = logger;
    }

    public ForwarderTablePageConfig GetPageConfig()
    {
        var options = _tableFieldsOptions.CurrentValue;
        var entries = options.List?.Fields ?? [];
        var fields = entries
            .Select(ResolveFieldMetadata)
            .Where(field => field.Visible)
            .ToList();

        if (fields.Count == 0)
        {
            _logger.LogWarning("forwarder-table-fields.json has no visible fields; using built-in defaults.");
            fields = BuildDefaultFields();
        }

        var filterFieldMap = fields
            .Where(field => field.Searchable)
            .ToDictionary(
                field => $"filter-{field.FieldName}",
                field => ResolveFilterQueryParam(field.FieldName),
                StringComparer.OrdinalIgnoreCase);

        return new ForwarderTablePageConfig
        {
            Fields = fields,
            TableUi = ForwarderTableUiOptions.MergeDefaults(options.TableUi),
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

    private static ForwarderTableFieldMetadata ResolveFieldMetadata(ForwarderTableFieldEntry entry)
    {
        var fieldName = entry.FieldName?.Trim() ?? string.Empty;
        return new ForwarderTableFieldMetadata
        {
            FieldName = fieldName,
            Visible = entry.Visible ?? true,
            Searchable = entry.Searchable ?? false,
            FilterType = string.IsNullOrWhiteSpace(entry.FilterType) ? "Checkbox" : entry.FilterType,
            HeaderLabel = DefaultHeaderLabels.TryGetValue(fieldName, out var label) ? label : fieldName
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

    private static List<ForwarderTableFieldMetadata> BuildDefaultFields() =>
        DefaultHeaderLabels
            .Select(pair => new ForwarderTableFieldMetadata
            {
                FieldName = pair.Key,
                Visible = true,
                Searchable = string.Equals(pair.Key, "DuplicateStatus", StringComparison.OrdinalIgnoreCase),
                FilterType = "Checkbox",
                HeaderLabel = pair.Value
            })
            .ToList();
}
