using System.ComponentModel.DataAnnotations;
using System.Reflection;
using ICP.Models.Icp;

namespace ICP.Models.ShipInfo;

public static class ShipInfoTableFieldCatalog
{
    public static readonly string[] HeaderFieldOrder =
    [
        "Id", "CreateTime", "CreateUser", "UpdateTime", "UpdateUser",
        "CreateDate", "Status", "SaDate", "InvoiceNo", "Forwarder", "Broker", "Etd", "Eta",
        "InvoiceDate", "Mawb", "Hawb", "Flt", "Freight", "DestinationPort", "DestinationCountry",
        "Warehouse", "InvoiceType", "Incoterms", "OrderType", "DeliveryDate", "DeliveryTo", "Bu",
        "TetPo", "OrderPriority", "MdpFlag", "TotalCartons", "NcdrNo", "NcdrRequestor",
        "EndUserCode", "EndUser", "RtNo", "Receiver", "Owner", "MachineNo", "MachineType",
        "ShipReason", "Forklift", "MovingLabor", "CarMethod", "ArriveTime", "WasteDisposal",
        "DriverDetails", "OrderReason", "ArrivalNoticeFlag", "ArrivalNotice",
        "ReasonForDeliveryDelay", "DelayNotificationDate", "DeliveryNo",
        "SoldToPartyCode", "SoldToParty", "ShipToPartyCode", "ShipToParty", "ShipToPartyAddress",
        "EmgFlight", "WbsElement", "Deposit", "DepositCaseStatus", "ArurCaseStatus",
        "SapRemarks", "Notes", "Cancellation", "ReasonForCancellation", "AttachedFile"
    ];

    public static readonly string[] DetailFieldOrder =
    [
        "Id", "CreateTime", "CreateUser", "UpdateTime", "UpdateUser",
        "InvoiceNo", "TetPo", "TetPoLine", "InvoiceSeq", "ItemNo", "Description", "Qty", "Uom", "Coo",
        "Price", "Amount", "Currency", "Rate", "PackingType", "CartonNo", "Length", "Width", "Hight",
        "GrossWeight", "NetWeightOfTheItem", "DeliveryLineNo", "Eccn", "ElFlag", "SdsFlag", "Hazmat",
        "DepositCaseStatus", "ArurCaseStatus"
    ];

    private static readonly Dictionary<string, PropertyInfo> HeaderProperties =
        BuildPropertyMap(typeof(IcpAuditableEntity), typeof(IcpHeader));

    private static readonly Dictionary<string, PropertyInfo> DetailProperties =
        BuildPropertyMap(typeof(IcpAuditableEntity), typeof(IcpDetail));

    private static readonly Dictionary<string, ShipInfoFieldSpec> HeaderSpecs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Status"] = new(ShipInfoControlTypes.Select, editable: true, searchable: true, lookupCategory: ShipInfoStatuses.LookupCategory, group: "Basic"),
        ["CreateDate"] = new(ShipInfoControlTypes.Text, editable: false, searchable: true, maxLength: 20, group: "Basic"),
        ["SaDate"] = new(ShipInfoControlTypes.DateRange, editable: true, searchable: true, maxLength: 10, group: "Shipping"),
        ["InvoiceNo"] = new(ShipInfoControlTypes.Text, editable: false, searchable: true, maxLength: 30, group: "Invoice"),
        ["Broker"] = new(ShipInfoControlTypes.Select, editable: false, searchable: true, lookupCategory: "Broker", maxLength: 30, group: "Customs"),
        ["Eta"] = new(ShipInfoControlTypes.DateRange, editable: true, searchable: true, maxLength: 10, group: "Shipping"),
        ["DeliveryTo"] = new(ShipInfoControlTypes.Select, editable: false, lookupCategory: "DeliveryToList", maxLength: 20, group: "Warehouse"),
        ["Notes"] = new(ShipInfoControlTypes.Textarea, editable: false, maxLength: 1000, group: "Other"),
        ["SapRemarks"] = new(ShipInfoControlTypes.Textarea, editable: false, maxLength: 1000, group: "Other"),
        ["CreateTime"] = new(ShipInfoControlTypes.DateTime, editable: false, group: "Audit"),
        ["UpdateTime"] = new(ShipInfoControlTypes.DateTime, editable: false, group: "Audit"),
    };

    private static readonly Dictionary<string, ShipInfoFieldSpec> DetailSpecs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["InvoiceSeq"] = new(ShipInfoControlTypes.Decimal, editable: false, group: "Basic"),
        ["Description"] = new(ShipInfoControlTypes.Text, editable: true, maxLength: 60, group: "Basic"),
        ["Qty"] = new(ShipInfoControlTypes.Decimal, editable: true, required: true, minValue: 0, group: "Basic"),
        ["Uom"] = new(ShipInfoControlTypes.Text, editable: true, maxLength: 10, group: "Basic"),
        ["Coo"] = new(ShipInfoControlTypes.Text, editable: true, maxLength: 50, group: "Basic"),
        ["CartonNo"] = new(ShipInfoControlTypes.Decimal, editable: true, group: "Packing"),
        ["GrossWeight"] = new(ShipInfoControlTypes.Decimal, editable: true, group: "Packing"),
        ["CreateTime"] = new(ShipInfoControlTypes.DateTime, editable: false, group: "Audit"),
        ["UpdateTime"] = new(ShipInfoControlTypes.DateTime, editable: false, group: "Audit"),
    };

    public static IReadOnlyList<ShipInfoFieldMetadata> BuildHeaderCatalog() =>
        BuildCatalog("header", HeaderFieldOrder, HeaderProperties, HeaderSpecs);

    public static IReadOnlyList<ShipInfoFieldMetadata> BuildDetailCatalog() =>
        BuildCatalog("detail", DetailFieldOrder, DetailProperties, DetailSpecs);

    private static IReadOnlyList<ShipInfoFieldMetadata> BuildCatalog(
        string tableKind,
        IReadOnlyList<string> fieldOrder,
        IReadOnlyDictionary<string, PropertyInfo> properties,
        IReadOnlyDictionary<string, ShipInfoFieldSpec> specs)
    {
        var fields = new List<ShipInfoFieldMetadata>(fieldOrder.Count);
        for (var index = 0; index < fieldOrder.Count; index++)
        {
            var fieldName = fieldOrder[index];
            properties.TryGetValue(fieldName, out var property);
            specs.TryGetValue(fieldName, out var spec);
            spec ??= ShipInfoFieldSpec.FromProperty(property);

            fields.Add(new ShipInfoFieldMetadata
            {
                Id = $"{tableKind}-{ToKebabCase(fieldName)}",
                FieldName = fieldName,
                DisplayName = HumanizeFieldName(fieldName),
                DisplayNameZh = HumanizeFieldName(fieldName),
                LabelKey = $"ShipInfo.Field.{fieldName}",
                DisplayOrder = (index + 1) * 10,
                ControlType = spec.ControlType,
                SearchControlType = spec.SearchControlType,
                LookupCategory = spec.LookupCategory,
                Searchable = spec.Searchable,
                FilterType = ShipInfoFilterTypes.Checkbox,
                Editable = spec.Editable,
                Required = spec.Required,
                Visible = true,
                MaxLength = spec.MaxLength,
                MinValue = spec.MinValue,
                Group = spec.Group
            });
        }

        return fields;
    }

    private static Dictionary<string, PropertyInfo> BuildPropertyMap(params Type[] types)
    {
        var map = new Dictionary<string, PropertyInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in types)
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (typeof(System.Collections.IEnumerable).IsAssignableFrom(property.PropertyType)
                    && property.PropertyType != typeof(string))
                {
                    continue;
                }

                map[property.Name] = property;
            }
        }

        return map;
    }

    private static string HumanizeFieldName(string fieldName) =>
        string.Concat(fieldName.Select((ch, index) =>
            index > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString()));

    private static string ToKebabCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var chars = new List<char>(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (char.IsUpper(current) && index > 0)
            {
                chars.Add('-');
            }

            chars.Add(char.ToLowerInvariant(current));
        }

        return new string(chars.ToArray());
    }

    private sealed class ShipInfoFieldSpec
    {
        public string ControlType { get; }
        public bool Searchable { get; }
        public bool Editable { get; }
        public bool Required { get; }
        public string? LookupCategory { get; }
        public string? SearchControlType { get; }
        public int? MaxLength { get; }
        public decimal? MinValue { get; }
        public string? Group { get; }

        public ShipInfoFieldSpec(
            string controlType,
            bool searchable = false,
            bool editable = false,
            bool required = false,
            string? lookupCategory = null,
            string? searchControlType = null,
            int? maxLength = null,
            decimal? minValue = null,
            string? group = null)
        {
            ControlType = controlType;
            Searchable = searchable;
            Editable = editable;
            Required = required;
            LookupCategory = lookupCategory;
            SearchControlType = searchControlType;
            MaxLength = maxLength;
            MinValue = minValue;
            Group = group;
        }

        public static ShipInfoFieldSpec FromProperty(PropertyInfo? property)
        {
            if (property is null)
            {
                return new ShipInfoFieldSpec(ShipInfoControlTypes.Text);
            }

            var maxLength = property.GetCustomAttribute<MaxLengthAttribute>()?.Length;
            var controlType = ResolveControlType(property.PropertyType);
            return new ShipInfoFieldSpec(controlType, maxLength: maxLength);
        }

        private static string ResolveControlType(Type propertyType)
        {
            var underlying = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            if (underlying == typeof(DateTime))
            {
                return ShipInfoControlTypes.DateTime;
            }

            if (underlying == typeof(decimal) || underlying == typeof(double) || underlying == typeof(float)
                || underlying == typeof(int) || underlying == typeof(long))
            {
                return ShipInfoControlTypes.Decimal;
            }

            if (underlying == typeof(Guid))
            {
                return ShipInfoControlTypes.Text;
            }

            return ShipInfoControlTypes.Text;
        }
    }
}
