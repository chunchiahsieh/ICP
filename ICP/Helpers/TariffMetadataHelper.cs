using ICP.Models.ShipInfo;
using ICP.Models.Tariff;

namespace ICP.Helpers;

public static class TariffMetadataHelper
{
    public static string ResolveFilterType(TariffTableFieldMetadata field) =>
        ShipInfoFilterTypes.Normalize(field.FilterType);

    public static bool IsCheckboxFilter(TariffTableFieldMetadata field) =>
        string.Equals(ResolveFilterType(field), ShipInfoFilterTypes.Checkbox, StringComparison.OrdinalIgnoreCase);

    public static bool IsVirtualField(string fieldName) =>
        string.Equals(fieldName, "RowNo", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fieldName, "DeclarationPdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fieldName, "CostFile", StringComparison.OrdinalIgnoreCase);

    public static bool IsAttachmentPresenceField(string fieldName) =>
        string.Equals(fieldName, "DeclarationPdf", StringComparison.OrdinalIgnoreCase)
        || string.Equals(fieldName, "CostFile", StringComparison.OrdinalIgnoreCase);
}
