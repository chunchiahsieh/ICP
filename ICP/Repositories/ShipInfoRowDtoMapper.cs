using System.Reflection;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.ShipInfo;

namespace ICP.Repositories;

public static class ShipInfoRowDtoMapper
{
    public static Dictionary<string, object?> MapHeader(IcpHeader header) =>
        ToDictionary(MapHeaderDto(header));

    public static Dictionary<string, object?> MapDetail(IcpDetail detail) =>
        ToDictionary(MapDetailDto(detail));

    public static void ApplyOutboxFailedFlags(
        Dictionary<string, object?> item,
        OutboxFailedFlags? flags)
    {
        item["DepositOutboxFailed"] = flags?.DepositFailed == true;
        item["ArurOutboxFailed"] = flags?.ArurFailed == true;
    }

    public static ShipInfoHeaderRowDto MapHeaderDto(IcpHeader header)
    {
        var headerKey = ShipInfoKeyHelper.BuildHeaderKey(header);
        var headerRowKey = ShipInfoKeyHelper.BuildHeaderRowKey(header);
        var status = ShipInfoStatusResolver.Resolve(header);

        return new ShipInfoHeaderRowDto
        {
            Id = headerRowKey,
            RowId = header.Id,
            HeaderKey = headerKey,
            HeaderRowKey = headerRowKey,
            CreateTime = header.CreateTime,
            CreateUser = header.CreateUser,
            UpdateTime = header.UpdateTime,
            UpdateUser = header.UpdateUser,
            CreateDate = header.CreateDate,
            Status = status,
            SaDate = header.SaDate,
            SaDateFrom = header.SaDate,
            InvoiceNo = header.InvoiceNo,
            Forwarder = header.Forwarder,
            Broker = header.Broker,
            Etd = header.Etd,
            Eta = header.Eta,
            EtaFrom = header.Eta,
            InvoiceDate = header.InvoiceDate,
            Mawb = header.Mawb,
            Hawb = header.Hawb,
            Flt = header.Flt,
            Flight = header.Flt,
            Freight = header.Freight,
            DestinationPort = header.DestinationPort,
            DestinationCountry = header.DestinationCountry,
            Warehouse = header.Warehouse,
            InvoiceType = header.InvoiceType,
            Incoterms = header.Incoterms,
            OrderType = header.OrderType,
            DeliveryDate = header.DeliveryDate,
            DeliveryTo = header.DeliveryTo,
            Bu = header.Bu,
            TetPo = header.TetPo,
            ShipNo = header.TetPo,
            OrderPriority = header.OrderPriority,
            MdpFlag = header.MdpFlag,
            TotalCartons = header.TotalCartons,
            NcdrNo = header.NcdrNo,
            NcdrRequestor = header.NcdrRequestor,
            EndUserCode = header.EndUserCode,
            EndUser = header.EndUser,
            Customer = header.EndUser ?? header.SoldToParty,
            RtNo = header.RtNo,
            ArurNo = header.RtNo,
            Receiver = header.Receiver,
            Owner = header.Owner,
            MachineNo = header.MachineNo,
            MachineType = header.MachineType,
            ShipReason = header.ShipReason,
            Forklift = header.Forklift,
            MovingLabor = header.MovingLabor,
            CarMethod = header.CarMethod,
            ArriveTime = header.ArriveTime,
            WasteDisposal = header.WasteDisposal,
            DriverDetails = header.DriverDetails,
            OrderReason = header.OrderReason,
            ArrivalNoticeFlag = header.ArrivalNoticeFlag,
            ArrivalNotice = header.ArrivalNotice,
            ReasonForDeliveryDelay = header.ReasonForDeliveryDelay,
            DelayNotificationDate = header.DelayNotificationDate,
            DeliveryNo = header.DeliveryNo,
            SoldToPartyCode = header.SoldToPartyCode,
            SoldToParty = header.SoldToParty,
            ShipToPartyCode = header.ShipToPartyCode,
            ShipToParty = header.ShipToParty,
            ShipToPartyAddress = header.ShipToPartyAddress,
            EmgFlight = header.EmgFlight,
            WbsElement = header.WbsElement,
            Deposit = header.Deposit,
            DepositNo = header.Deposit,
            DepositCaseStatus = ShipInfoCaseStatusResolver.Normalize(header.DepositCaseStatus),
            ArurCaseStatus = ShipInfoCaseStatusResolver.Normalize(header.ArurCaseStatus),
            SapRemarks = header.SapRemarks,
            Notes = header.Notes,
            Remark = header.Notes ?? header.SapRemarks,
            Cancellation = header.Cancellation,
            ReasonForCancellation = header.ReasonForCancellation,
            AttachedFile = header.AttachedFile
        };
    }

    public static ShipInfoDetailRowDto MapDetailDto(IcpDetail detail)
    {
        var detailKey = ShipInfoKeyHelper.BuildDetailKey(detail);
        var headerKey = ShipInfoKeyHelper.BuildHeaderKey(detail.InvoiceNo);
        var weight = detail.GrossWeight ?? (detail.NetWeightOfTheItem is null
            ? (decimal?)null
            : Convert.ToDecimal(detail.NetWeightOfTheItem));

        return new ShipInfoDetailRowDto
        {
            Id = detailKey,
            RowId = detail.Id,
            DetailKey = detailKey,
            HeaderKey = headerKey,
            CreateTime = detail.CreateTime,
            CreateUser = detail.CreateUser,
            UpdateTime = detail.UpdateTime,
            UpdateUser = detail.UpdateUser,
            InvoiceNo = detail.InvoiceNo,
            TetPo = detail.TetPo,
            TetPoLine = detail.TetPoLine,
            InvoiceSeq = detail.InvoiceSeq,
            LineNo = detail.InvoiceSeq,
            ItemNo = detail.ItemNo,
            MaterialCode = detail.ItemNo,
            Description = detail.Description,
            Qty = detail.Qty,
            Quantity = detail.Qty,
            InvoiceQty = detail.Qty,
            Uom = detail.Uom,
            Coo = detail.Coo,
            Price = detail.Price,
            Amount = detail.Amount,
            Currency = detail.Currency,
            Rate = detail.Rate,
            PackingType = detail.PackingType,
            CartonNo = detail.CartonNo,
            Carton = detail.CartonNo,
            Length = detail.Length,
            Width = detail.Width,
            Hight = detail.Hight,
            GrossWeight = detail.GrossWeight,
            Weight = weight,
            NetWeightOfTheItem = detail.NetWeightOfTheItem,
            DeliveryLineNo = detail.DeliveryLineNo,
            Eccn = detail.Eccn,
            ElFlag = detail.ElFlag,
            SdsFlag = detail.SdsFlag,
            Hazmat = detail.Hazmat,
            DepositCaseStatus = ShipInfoCaseStatusResolver.Normalize(detail.DepositCaseStatus),
            ArurCaseStatus = ShipInfoCaseStatusResolver.Normalize(detail.ArurCaseStatus)
        };
    }

    private static Dictionary<string, object?> ToDictionary(object dto)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in dto.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            result[property.Name] = property.GetValue(dto);
        }

        return result;
    }
}
