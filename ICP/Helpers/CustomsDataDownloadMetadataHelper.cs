using ICP.Models.CustomsDataDownload;
using ICP.Models.ShipInfo;

namespace ICP.Helpers;

public static class CustomsDataDownloadMetadataHelper
{
    public static string ResolveFilterType(CustomsDataDownloadTableFieldMetadata field) =>
        ShipInfoFilterTypes.Normalize(field.FilterType);

    public static bool IsCheckboxFilter(CustomsDataDownloadTableFieldMetadata field) =>
        string.Equals(ResolveFilterType(field), ShipInfoFilterTypes.Checkbox, StringComparison.OrdinalIgnoreCase);
}
