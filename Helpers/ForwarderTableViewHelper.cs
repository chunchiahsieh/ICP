using ICP.Models;
using ICP.Models.Forwarder;
using ICP.Models.Icp;

namespace ICP.Helpers;

public static class ForwarderTableViewHelper
{
    public static string ResolveHeaderLabel(ForwarderTableFieldMetadata field, Func<string, string> localizeDuplicate)
    {
        if (string.Equals(field.FieldName, "DuplicateStatus", StringComparison.OrdinalIgnoreCase))
        {
            return localizeDuplicate("Forwarder.ForwarderDataUpload.Column.Duplicate");
        }

        if (string.Equals(field.FieldName, "RowNo", StringComparison.OrdinalIgnoreCase))
        {
            return localizeDuplicate("Forwarder.ForwarderDataUpload.Column.RowNo");
        }

        return field.HeaderLabel;
    }

    public static string FormatCellValue(ForwarderDataUploadRowViewModel item, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)
            || string.Equals(fieldName, "DuplicateStatus", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        if (string.Equals(fieldName, "RowNo", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var row = item.Row;
        return fieldName switch
        {
            nameof(ForwarderDataUpload.Type) => row.Type,
            nameof(ForwarderDataUpload.InvoiceNo) => row.InvoiceNo,
            nameof(ForwarderDataUpload.CustomerReference) => row.CustomerReference ?? string.Empty,
            nameof(ForwarderDataUpload.MaterialCode) => row.MaterialCode ?? string.Empty,
            nameof(ForwarderDataUpload.OrderMaterialName) => row.OrderMaterialName ?? string.Empty,
            nameof(ForwarderDataUpload.Quantity) => row.Quantity?.ToString("0.####") ?? string.Empty,
            nameof(ForwarderDataUpload.PortOfLoading) => row.PortOfLoading ?? string.Empty,
            nameof(ForwarderDataUpload.ShipToName) => row.ShipToName ?? string.Empty,
            nameof(ForwarderDataUpload.ShipToAddress) => row.ShipToAddress ?? string.Empty,
            nameof(ForwarderDataUpload.ShipToPartyCountryCode) => row.ShipToPartyCountryCode ?? string.Empty,
            nameof(ForwarderDataUpload.ShipToPortCode) => row.ShipToPortCode ?? string.Empty,
            nameof(ForwarderDataUpload.FreightCharge) => row.FreightCharge ?? string.Empty,
            nameof(ForwarderDataUpload.ConfirmedCustomDate) => row.ConfirmedCustomDate?.ToString("yyyy/MM/dd") ?? string.Empty,
            nameof(ForwarderDataUpload.Hawb) => row.Hawb ?? string.Empty,
            nameof(ForwarderDataUpload.Mawb) => row.Mawb ?? string.Empty,
            nameof(ForwarderDataUpload.Etd) => row.Etd?.ToString("yyyy/MM/dd") ?? string.Empty,
            nameof(ForwarderDataUpload.Eta) => row.Eta?.ToString("yyyy/MM/dd") ?? string.Empty,
            nameof(ForwarderDataUpload.Flight1) => row.Flight1 ?? string.Empty,
            nameof(ForwarderDataUpload.Flight2) => row.Flight2 ?? string.Empty,
            nameof(ForwarderDataUpload.Cb) => row.Cb ?? string.Empty,
            nameof(ForwarderDataUpload.Action) => row.Action ?? string.Empty,
            nameof(ForwarderDataUpload.Mdp) => row.Mdp ?? string.Empty,
            _ => string.Empty
        };
    }

    public static string ResolveRowClass(ForwarderDataUploadRowViewModel item) =>
        item.IsDbDuplicate
            ? "forwarder-row-db-duplicate"
            : item.IsInFileMultiLine
                ? "forwarder-row-infile-multiline"
                : string.Empty;
}
