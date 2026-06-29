using System.Globalization;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;

namespace ICP.Services;

public static class ShipInfoDetailSummaryCalculator
{
    public static ShipInfoDetailSummaryDto Calculate(IReadOnlyList<IcpDetail> details)
    {
        var hasWeight = details.Any(x => x.GrossWeight.HasValue || x.NetWeightOfTheItem.HasValue);
        var hasCarton = details.Any(x => x.CartonNo.HasValue);

        return new ShipInfoDetailSummaryDto
        {
            DetailCount = details.Count,
            TotalQty = details.Sum(x => x.Qty ?? 0m),
            TotalWeight = hasWeight
                ? details.Sum(x => x.GrossWeight ?? (decimal?)(x.NetWeightOfTheItem ?? 0d) ?? 0m)
                : null,
            TotalInvoiceQty = details.Any(x => x.Qty.HasValue) ? details.Sum(x => x.Qty ?? 0m) : null,
            TotalCarton = hasCarton ? (int?)details.Sum(x => x.CartonNo ?? 0d) : null
        };
    }

    public static ShipInfoHeaderSummaryDto BuildHeaderSummary(IcpHeader header)
    {
        return new ShipInfoHeaderSummaryDto
        {
            ShipNo = header.TetPo,
            InvoiceNo = header.InvoiceNo,
            Status = ShipInfoStatusResolver.Resolve(header),
            Broker = header.Broker,
            Customer = header.EndUser ?? header.SoldToParty,
            SaDate = header.SaDate
        };
    }
}
