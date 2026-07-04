using ICP.Models.Icp;
using ICP.Models.Tariff;

namespace ICP.Helpers;

public static class TariffTableViewHelper
{
    public static string ResolveHeaderLabel(TariffTableFieldMetadata field, Func<string, string> localize) =>
        localize(field.HeaderLabelKey);

    public static string FormatCellValue(TariffData item, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(fieldName)
            || string.Equals(fieldName, "RowNo", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return fieldName switch
        {
            nameof(TariffData.MAWB) => item.MAWB,
            nameof(TariffData.HAWB) => item.HAWB,
            nameof(TariffData.ImportDate) => item.ImportDate.ToString("yyyy/MM/dd"),
            nameof(TariffData.DeclarationDate) => item.DeclarationDate.ToString("yyyy/MM/dd"),
            nameof(TariffData.ReleaseDate) => item.ReleaseDate.ToString("yyyy/MM/dd"),
            nameof(TariffData.InvoiceNumber) => item.InvoiceNumber,
            nameof(TariffData.DescriptionOfGoods) => item.DescriptionOfGoods,
            nameof(TariffData.HTSNumber) => item.HTSNumber,
            nameof(TariffData.EntryNumber) => item.EntryNumber,
            nameof(TariffData.Mode) => item.Mode,
            nameof(TariffData.PortOfDeparture) => item.PortOfDeparture,
            nameof(TariffData.FlightNo) => item.FlightNo,
            nameof(TariffData.Shipper) => item.Shipper ?? string.Empty,
            nameof(TariffData.Broker) => item.Broker ?? string.Empty,
            nameof(TariffData.AirSea) => item.AirSea,
            nameof(TariffData.CreateDate) => item.CreateDate.ToString("yyyy/MM/dd"),
            _ => string.Empty
        };
    }
}
