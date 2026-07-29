namespace ICP.Helpers;

public static class TariffExcelColumnMap
{
    public const string MAWB = nameof(ICP.Models.Icp.TariffData.MAWB);
    public const string HAWB = nameof(ICP.Models.Icp.TariffData.HAWB);
    public const string ImportDate = nameof(ICP.Models.Icp.TariffData.ImportDate);
    public const string DeclarationDate = nameof(ICP.Models.Icp.TariffData.DeclarationDate);
    public const string ReleaseDate = nameof(ICP.Models.Icp.TariffData.ReleaseDate);
    public const string LineNo = nameof(ICP.Models.Icp.TariffData.LineNo);
    public const string PartNumber = nameof(ICP.Models.Icp.TariffData.PartNumber);
    public const string InvoiceNumber = nameof(ICP.Models.Icp.TariffData.InvoiceNumber);
    public const string PONumber = nameof(ICP.Models.Icp.TariffData.PONumber);
    public const string DescriptionOfGoods = nameof(ICP.Models.Icp.TariffData.DescriptionOfGoods);
    public const string Quantity = nameof(ICP.Models.Icp.TariffData.Quantity);
    public const string UOM = nameof(ICP.Models.Icp.TariffData.UOM);
    public const string NetWeightKg = nameof(ICP.Models.Icp.TariffData.NetWeightKg);
    public const string UnitValue = nameof(ICP.Models.Icp.TariffData.UnitValue);
    public const string HTSNumber = nameof(ICP.Models.Icp.TariffData.HTSNumber);
    public const string COO = nameof(ICP.Models.Icp.TariffData.COO);
    public const string DutyRate = nameof(ICP.Models.Icp.TariffData.DutyRate);
    public const string DutyTreatment = nameof(ICP.Models.Icp.TariffData.DutyTreatment);
    public const string PermitNo1 = nameof(ICP.Models.Icp.TariffData.PermitNo1);
    public const string PermitItem1 = nameof(ICP.Models.Icp.TariffData.PermitItem1);
    public const string PermitNo2 = nameof(ICP.Models.Icp.TariffData.PermitNo2);
    public const string PermitItem2 = nameof(ICP.Models.Icp.TariffData.PermitItem2);
    public const string PermitNo3 = nameof(ICP.Models.Icp.TariffData.PermitNo3);
    public const string PermitItem3 = nameof(ICP.Models.Icp.TariffData.PermitItem3);
    public const string EntryNumber = nameof(ICP.Models.Icp.TariffData.EntryNumber);
    public const string Type = nameof(ICP.Models.Icp.TariffData.Type);
    public const string Mode = nameof(ICP.Models.Icp.TariffData.Mode);
    public const string PortOfDeparture = nameof(ICP.Models.Icp.TariffData.PortOfDeparture);
    public const string FlightNo = nameof(ICP.Models.Icp.TariffData.FlightNo);
    public const string Shipper = nameof(ICP.Models.Icp.TariffData.Shipper);
    public const string TermsOfTrade = nameof(ICP.Models.Icp.TariffData.TermsOfTrade);
    public const string Currency = nameof(ICP.Models.Icp.TariffData.Currency);
    public const string ExchangeRate = nameof(ICP.Models.Icp.TariffData.ExchangeRate);
    public const string CIFValue = nameof(ICP.Models.Icp.TariffData.CIFValue);
    public const string FreightCharge = nameof(ICP.Models.Icp.TariffData.FreightCharge);
    public const string TotalPieces = nameof(ICP.Models.Icp.TariffData.TotalPieces);
    public const string GrossWeightKg = nameof(ICP.Models.Icp.TariffData.GrossWeightKg);
    public const string Broker = nameof(ICP.Models.Icp.TariffData.Broker);
    public const string AirSea = nameof(ICP.Models.Icp.TariffData.AirSea);
    public const string TotalAmountForeignCurrency = nameof(ICP.Models.Icp.TariffData.TotalAmountForeignCurrency);
    public const string TotalAmountTWD = nameof(ICP.Models.Icp.TariffData.TotalAmountTWD);
    public const string DeclarationAmountTWD = nameof(ICP.Models.Icp.TariffData.DeclarationAmountTWD);

    private static readonly Dictionary<string, string> HeaderAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["MAWB"] = MAWB,
        ["Master Bill"] = MAWB,
        ["HAWB"] = HAWB,
        ["House Bill"] = HAWB,
        ["Import Date"] = ImportDate,
        ["Declaration Date"] = DeclarationDate,
        ["Release Date"] = ReleaseDate,
        ["Line No"] = LineNo,
        ["Line No."] = LineNo,
        ["Part Number"] = PartNumber,
        ["Invoice Number"] = InvoiceNumber,
        ["Invoice No"] = InvoiceNumber,
        ["Invoice No."] = InvoiceNumber,
        ["PO Number"] = PONumber,
        ["Description Of Goods"] = DescriptionOfGoods,
        ["Description of Goods"] = DescriptionOfGoods,
        ["Quantity"] = Quantity,
        ["UOM"] = UOM,
        ["Net Weight(KG)"] = NetWeightKg,
        ["Net Weight Kg"] = NetWeightKg,
        ["Net Weight(Kg)"] = NetWeightKg,
        ["Unit Value"] = UnitValue,
        ["HTS Number"] = HTSNumber,
        ["HTS Code"] = HTSNumber,
        ["COO"] = COO,
        ["Duty Rate"] = DutyRate,
        ["Duty Treatment"] = DutyTreatment,
        ["Permit No.-1"] = PermitNo1,
        ["Permit No.1"] = PermitNo1,
        ["Permit Item-1"] = PermitItem1,
        ["Permit Item.1"] = PermitItem1,
        ["Permit No.-2"] = PermitNo2,
        ["Permit No.2"] = PermitNo2,
        ["Permit Item-2"] = PermitItem2,
        ["Permit Item.2"] = PermitItem2,
        ["Permit No.-3"] = PermitNo3,
        ["Permit No.3"] = PermitNo3,
        ["Permit Item-3"] = PermitItem3,
        ["Permit Item.3"] = PermitItem3,
        ["Entry Number"] = EntryNumber,
        ["Type"] = Type,
        ["Mode"] = Mode,
        ["Port of departure"] = PortOfDeparture,
        ["Port of Departure"] = PortOfDeparture,
        ["Flight No."] = FlightNo,
        ["Flight No"] = FlightNo,
        ["Shipper"] = Shipper,
        ["Terms of Trade"] = TermsOfTrade,
        ["Terms Of Trade"] = TermsOfTrade,
        ["Currency"] = Currency,
        ["Exchange Rate"] = ExchangeRate,
        ["CIF Value"] = CIFValue,
        ["Freight Charge"] = FreightCharge,
        ["Total Pieces"] = TotalPieces,
        ["Gross Weight(KG)"] = GrossWeightKg,
        ["Gross Weight Kg"] = GrossWeightKg,
        ["Gross Weight(Kg)"] = GrossWeightKg,
        ["Broker"] = Broker,
        ["Air/Sea"] = AirSea,
        ["Air Sea"] = AirSea,
        ["Total Amount Foreign Currency"] = TotalAmountForeignCurrency,
        ["Total Amount TWD"] = TotalAmountTWD,
        ["Declaration Amount (TWD)"] = DeclarationAmountTWD,
        ["Declaration Amount TWD"] = DeclarationAmountTWD,
        ["報單台幣金額(TWD)"] = DeclarationAmountTWD,
        ["報單台幣金額 (TWD)"] = DeclarationAmountTWD,
        ["洋通方式"] = Mode,
        ["空海運"] = AirSea,
        ["主提單號碼"] = MAWB,
        ["分提單號碼"] = HAWB,
        ["進口日期"] = ImportDate,
        ["報關日期"] = DeclarationDate,
        ["放行日期"] = ReleaseDate,
        ["項次"] = LineNo,
        ["料號"] = PartNumber,
        ["報單號碼"] = EntryNumber,
        ["報單類別"] = Type,
        ["出口港/代碼"] = PortOfDeparture,
        ["離岸港/代碼"] = PortOfDeparture,
        ["離岸港代碼/名稱"] = PortOfDeparture,
        ["船名航次/航機班次"] = FlightNo,
        ["船舶航次/班機航次"] = FlightNo,
        ["出口方"] = Shipper,
        ["出貨方"] = Shipper,
        ["貿易條件"] = TermsOfTrade,
        ["幣別"] = Currency,
        ["匯率"] = ExchangeRate,
        ["報關行"] = Broker
    };

    private static readonly HashSet<string> RequiredColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        MAWB,
        ImportDate,
        DeclarationDate,
        ReleaseDate,
        LineNo,
        PartNumber,
        InvoiceNumber,
        PONumber,
        DescriptionOfGoods,
        Quantity,
        UOM,
        NetWeightKg,
        UnitValue,
        HTSNumber,
        COO,
        DutyRate,
        DutyTreatment,
        EntryNumber,
        Type,
        Mode,
        PortOfDeparture,
        FlightNo,
        Shipper,
        TermsOfTrade,
        Currency,
        ExchangeRate,
        CIFValue,
        FreightCharge,
        TotalPieces,
        GrossWeightKg,
        AirSea,
        DeclarationAmountTWD
    };

    private static readonly Dictionary<string, string> DisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [MAWB] = "Master Bill",
        [HAWB] = "House Bill",
        [ImportDate] = "Import Date",
        [DeclarationDate] = "Declaration Date",
        [ReleaseDate] = "Release Date",
        [LineNo] = "Line No.",
        [PartNumber] = "Part Number",
        [InvoiceNumber] = "Invoice Number",
        [PONumber] = "PO Number",
        [DescriptionOfGoods] = "Description Of Goods",
        [Quantity] = "Quantity",
        [UOM] = "UOM",
        [NetWeightKg] = "Net Weight(KG)",
        [UnitValue] = "Unit Value",
        [HTSNumber] = "HTS Number",
        [COO] = "COO",
        [DutyRate] = "Duty Rate",
        [DutyTreatment] = "Duty Treatment",
        [PermitNo1] = "Permit No.-1",
        [PermitItem1] = "Permit Item-1",
        [PermitNo2] = "Permit No.-2",
        [PermitItem2] = "Permit Item-2",
        [PermitNo3] = "Permit No.-3",
        [PermitItem3] = "Permit Item-3",
        [EntryNumber] = "Entry Number",
        [Type] = "Type",
        [Mode] = "Mode",
        [PortOfDeparture] = "Port of departure",
        [FlightNo] = "Flight No.",
        [Shipper] = "Shipper",
        [TermsOfTrade] = "Terms of Trade",
        [Currency] = "Currency",
        [ExchangeRate] = "Exchange Rate",
        [CIFValue] = "CIF Value",
        [FreightCharge] = "Freight Charge",
        [TotalPieces] = "Total Pieces",
        [GrossWeightKg] = "Gross Weight(KG)",
        [Broker] = "Broker",
        [AirSea] = "Air/Sea",
        [TotalAmountForeignCurrency] = "Total Amount Foreign Currency",
        [TotalAmountTWD] = "Total Amount TWD",
        [DeclarationAmountTWD] = "Declaration Amount (TWD)",
        [nameof(ICP.Models.Icp.TariffData.ImportFileName)] = "Import File Name"
    };

    public static bool IsRequiredColumn(string propertyName) =>
        RequiredColumns.Contains(propertyName);

    public static string GetDisplayName(string propertyName) =>
        DisplayNames.TryGetValue(propertyName, out var displayName) ? displayName : propertyName;

    public static bool TryResolveProperty(string? header, out string propertyName)
    {
        propertyName = string.Empty;
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var normalized = NormalizeHeader(header);
        return HeaderAliases.TryGetValue(normalized, out propertyName!);
    }

    private static string NormalizeHeader(string header)
    {
        var normalized = header.Trim().Replace('\u00A0', ' ');
        while (normalized.StartsWith('*'))
        {
            normalized = normalized[1..].TrimStart();
        }

        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        }

        return normalized;
    }
}
