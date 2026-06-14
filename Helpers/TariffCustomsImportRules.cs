using System.Globalization;
using System.Text.RegularExpressions;
using ICP.Models;
using ICP.Models.Icp;

namespace ICP.Helpers;

public static class TariffCustomsImportRules
{
    private static readonly string[] RequiredHeaderColumns =
    [
        TariffExcelColumnMap.InvoiceNumber,
        TariffExcelColumnMap.MAWB,
        TariffExcelColumnMap.HAWB,
        TariffExcelColumnMap.AirSea,
        TariffExcelColumnMap.DeclarationAmountTWD
    ];

    private static readonly HashSet<string> RequiredCellColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        TariffExcelColumnMap.MAWB,
        TariffExcelColumnMap.ImportDate,
        TariffExcelColumnMap.DeclarationDate,
        TariffExcelColumnMap.ReleaseDate,
        TariffExcelColumnMap.LineNo,
        TariffExcelColumnMap.PartNumber,
        TariffExcelColumnMap.InvoiceNumber,
        TariffExcelColumnMap.PONumber,
        TariffExcelColumnMap.DescriptionOfGoods,
        TariffExcelColumnMap.Quantity,
        TariffExcelColumnMap.UOM,
        TariffExcelColumnMap.NetWeightKg,
        TariffExcelColumnMap.UnitValue,
        TariffExcelColumnMap.HTSNumber,
        TariffExcelColumnMap.COO,
        TariffExcelColumnMap.DutyRate,
        TariffExcelColumnMap.DutyTreatment,
        TariffExcelColumnMap.EntryNumber,
        TariffExcelColumnMap.Type,
        TariffExcelColumnMap.Mode,
        TariffExcelColumnMap.PortOfDeparture,
        TariffExcelColumnMap.FlightNo,
        TariffExcelColumnMap.Shipper,
        TariffExcelColumnMap.TermsOfTrade,
        TariffExcelColumnMap.Currency,
        TariffExcelColumnMap.ExchangeRate,
        TariffExcelColumnMap.CIFValue,
        TariffExcelColumnMap.FreightCharge,
        TariffExcelColumnMap.TotalPieces,
        TariffExcelColumnMap.GrossWeightKg,
        TariffExcelColumnMap.AirSea,
        TariffExcelColumnMap.DeclarationAmountTWD
    };

    private static readonly Dictionary<string, int> MaxLengths = new(StringComparer.OrdinalIgnoreCase)
    {
        [TariffExcelColumnMap.MAWB] = 50,
        [TariffExcelColumnMap.HAWB] = 50,
        [TariffExcelColumnMap.LineNo] = 50,
        [TariffExcelColumnMap.PartNumber] = 100,
        [TariffExcelColumnMap.InvoiceNumber] = 100,
        [TariffExcelColumnMap.PONumber] = 100,
        [TariffExcelColumnMap.DescriptionOfGoods] = 200,
        [TariffExcelColumnMap.Quantity] = 50,
        [TariffExcelColumnMap.UOM] = 50,
        [TariffExcelColumnMap.NetWeightKg] = 50,
        [TariffExcelColumnMap.UnitValue] = 50,
        [TariffExcelColumnMap.HTSNumber] = 50,
        [TariffExcelColumnMap.COO] = 50,
        [TariffExcelColumnMap.DutyRate] = 50,
        [TariffExcelColumnMap.DutyTreatment] = 100,
        [TariffExcelColumnMap.PermitNo1] = 100,
        [TariffExcelColumnMap.PermitItem1] = 100,
        [TariffExcelColumnMap.PermitNo2] = 100,
        [TariffExcelColumnMap.PermitItem2] = 100,
        [TariffExcelColumnMap.PermitNo3] = 100,
        [TariffExcelColumnMap.PermitItem3] = 100,
        [TariffExcelColumnMap.EntryNumber] = 100,
        [TariffExcelColumnMap.Type] = 50,
        [TariffExcelColumnMap.Mode] = 50,
        [TariffExcelColumnMap.PortOfDeparture] = 100,
        [TariffExcelColumnMap.FlightNo] = 100,
        [TariffExcelColumnMap.Shipper] = 200,
        [TariffExcelColumnMap.TermsOfTrade] = 50,
        [TariffExcelColumnMap.Currency] = 50,
        [TariffExcelColumnMap.ExchangeRate] = 50,
        [TariffExcelColumnMap.CIFValue] = 50,
        [TariffExcelColumnMap.FreightCharge] = 50,
        [TariffExcelColumnMap.TotalPieces] = 50,
        [TariffExcelColumnMap.GrossWeightKg] = 50,
        [TariffExcelColumnMap.Broker] = 200,
        [TariffExcelColumnMap.AirSea] = 50,
        [TariffExcelColumnMap.DeclarationAmountTWD] = 50
    };

    public static IReadOnlyList<string> RequiredHeaderColumnProperties => RequiredHeaderColumns;

    public static bool IsRequiredCellColumn(string propertyName) =>
        RequiredCellColumns.Contains(propertyName);

    public static void ValidateRequiredHeaders(IReadOnlyDictionary<string, int> columnMap, List<string> errors)
    {
        foreach (var propertyName in RequiredHeaderColumns)
        {
            if (!columnMap.ContainsKey(propertyName))
            {
                errors.Add($"標題列缺少必填欄位 {TariffExcelColumnMap.GetDisplayName(propertyName)}");
            }
        }
    }

    public static string ResolveBroker(string importFileName, TariffDataOptions options)
    {
        var fileName = Path.GetFileName(importFileName);
        var upper = fileName.ToUpperInvariant();

        if (ContainsAnyKeyword(upper, options.BrokerKeywords.KWE))
        {
            return "KWE";
        }

        if (ContainsAnyKeyword(upper, options.BrokerKeywords.YUANFAN))
        {
            return "YUANFAN";
        }

        throw new InvalidOperationException($"無法依檔名判斷報關行（Broker）：{fileName}");
    }

    public static string ResolveHawb(string? hawb, string mawb) =>
        string.IsNullOrEmpty(NormalizeCellText(hawb)) ? mawb : NormalizeCellText(hawb)!;

    public static void ValidateNewRowHawbs(
        IEnumerable<TariffData> rows,
        IReadOnlySet<string> existingInvoiceNumbers,
        IReadOnlySet<string> knownHawbs,
        bool tariffDataExists,
        List<string> errors)
    {
        if (!tariffDataExists)
        {
            return;
        }

        foreach (var row in rows)
        {
            if (existingInvoiceNumbers.Contains(row.InvoiceNumber))
            {
                continue;
            }

            if (!knownHawbs.Contains(row.HAWB))
            {
                errors.Add($"Invoice Number {row.InvoiceNumber} 的 HAWB {row.HAWB} 不存在於 ICP 關稅資料");
            }
        }
    }

    public static TariffData MapRow(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columnMap,
        string importFileName,
        Guid importBatchId,
        DateOnly createDate,
        string broker,
        int rowNumber,
        List<string> errors)
    {
        var mawb = RequireCellValue(values, columnMap, TariffExcelColumnMap.MAWB, rowNumber, errors);
        var hawbRaw = GetCellValue(values, columnMap, TariffExcelColumnMap.HAWB);
        var hawb = ValidateAndTrim(
            ResolveHawb(hawbRaw, mawb ?? string.Empty),
            TariffExcelColumnMap.HAWB,
            rowNumber,
            errors,
            required: false);

        RequireCellValue(values, columnMap, TariffExcelColumnMap.ImportDate, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.DeclarationDate, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.ReleaseDate, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.LineNo, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.PartNumber, rowNumber, errors);
        var invoiceNumber = RequireCellValue(values, columnMap, TariffExcelColumnMap.InvoiceNumber, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.PONumber, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.DescriptionOfGoods, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.Quantity, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.UOM, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.NetWeightKg, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.UnitValue, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.HTSNumber, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.COO, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.DutyRate, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.DutyTreatment, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.EntryNumber, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.Type, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.Mode, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.PortOfDeparture, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.FlightNo, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.Shipper, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.TermsOfTrade, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.Currency, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.ExchangeRate, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.CIFValue, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.FreightCharge, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.TotalPieces, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.GrossWeightKg, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.AirSea, rowNumber, errors);
        RequireCellValue(values, columnMap, TariffExcelColumnMap.DeclarationAmountTWD, rowNumber, errors);

        var unitValue = ParseRequiredDecimal(values, columnMap, TariffExcelColumnMap.UnitValue, rowNumber, errors);
        var quantity = ParseRequiredDecimal(values, columnMap, TariffExcelColumnMap.Quantity, rowNumber, errors);
        var exchangeRate = ParseRequiredDecimal(values, columnMap, TariffExcelColumnMap.ExchangeRate, rowNumber, errors);

        decimal? totalAmountForeignCurrency = null;
        decimal? totalAmountTwd = null;
        if (unitValue.HasValue && quantity.HasValue)
        {
            totalAmountForeignCurrency = unitValue.Value * quantity.Value;
            if (exchangeRate.HasValue)
            {
                totalAmountTwd = totalAmountForeignCurrency.Value * exchangeRate.Value;
            }
        }

        return new TariffData
        {
            MAWB = ValidateAndTrim(mawb, TariffExcelColumnMap.MAWB, rowNumber, errors)!,
            HAWB = hawb ?? string.Empty,
            ImportDate = ParseRequiredDateOnly(values, columnMap, TariffExcelColumnMap.ImportDate, rowNumber, errors),
            DeclarationDate = ParseRequiredDateOnly(values, columnMap, TariffExcelColumnMap.DeclarationDate, rowNumber, errors),
            ReleaseDate = ParseRequiredDateOnly(values, columnMap, TariffExcelColumnMap.ReleaseDate, rowNumber, errors),
            LineNo = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.LineNo), TariffExcelColumnMap.LineNo, rowNumber, errors)!,
            PartNumber = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PartNumber), TariffExcelColumnMap.PartNumber, rowNumber, errors)!,
            InvoiceNumber = ValidateAndTrim(invoiceNumber, TariffExcelColumnMap.InvoiceNumber, rowNumber, errors)!,
            PONumber = ValidateOptionalTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PONumber), TariffExcelColumnMap.PONumber, rowNumber, errors),
            DescriptionOfGoods = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.DescriptionOfGoods), TariffExcelColumnMap.DescriptionOfGoods, rowNumber, errors)!,
            Quantity = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.Quantity), TariffExcelColumnMap.Quantity, rowNumber, errors)!,
            UOM = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.UOM), TariffExcelColumnMap.UOM, rowNumber, errors)!,
            NetWeightKg = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.NetWeightKg), TariffExcelColumnMap.NetWeightKg, rowNumber, errors)!,
            UnitValue = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.UnitValue), TariffExcelColumnMap.UnitValue, rowNumber, errors)!,
            HTSNumber = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.HTSNumber), TariffExcelColumnMap.HTSNumber, rowNumber, errors)!,
            COO = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.COO), TariffExcelColumnMap.COO, rowNumber, errors)!,
            DutyRate = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.DutyRate), TariffExcelColumnMap.DutyRate, rowNumber, errors)!,
            DutyTreatment = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.DutyTreatment), TariffExcelColumnMap.DutyTreatment, rowNumber, errors)!,
            PermitNo1 = ValidateOptionalTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PermitNo1), TariffExcelColumnMap.PermitNo1, rowNumber, errors),
            PermitItem1 = ValidateOptionalTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PermitItem1), TariffExcelColumnMap.PermitItem1, rowNumber, errors),
            PermitNo2 = ValidateOptionalTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PermitNo2), TariffExcelColumnMap.PermitNo2, rowNumber, errors),
            PermitItem2 = ValidateOptionalTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PermitItem2), TariffExcelColumnMap.PermitItem2, rowNumber, errors),
            PermitNo3 = ValidateOptionalTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PermitNo3), TariffExcelColumnMap.PermitNo3, rowNumber, errors),
            PermitItem3 = ValidateOptionalTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PermitItem3), TariffExcelColumnMap.PermitItem3, rowNumber, errors),
            EntryNumber = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.EntryNumber), TariffExcelColumnMap.EntryNumber, rowNumber, errors)!,
            Type = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.Type), TariffExcelColumnMap.Type, rowNumber, errors)!,
            Mode = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.Mode), TariffExcelColumnMap.Mode, rowNumber, errors)!,
            PortOfDeparture = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.PortOfDeparture), TariffExcelColumnMap.PortOfDeparture, rowNumber, errors)!,
            FlightNo = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.FlightNo), TariffExcelColumnMap.FlightNo, rowNumber, errors)!,
            Shipper = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.Shipper), TariffExcelColumnMap.Shipper, rowNumber, errors)!,
            TermsOfTrade = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.TermsOfTrade), TariffExcelColumnMap.TermsOfTrade, rowNumber, errors)!,
            Currency = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.Currency), TariffExcelColumnMap.Currency, rowNumber, errors)!,
            ExchangeRate = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.ExchangeRate), TariffExcelColumnMap.ExchangeRate, rowNumber, errors)!,
            CIFValue = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.CIFValue), TariffExcelColumnMap.CIFValue, rowNumber, errors)!,
            FreightCharge = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.FreightCharge), TariffExcelColumnMap.FreightCharge, rowNumber, errors)!,
            TotalPieces = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.TotalPieces), TariffExcelColumnMap.TotalPieces, rowNumber, errors)!,
            GrossWeightKg = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.GrossWeightKg), TariffExcelColumnMap.GrossWeightKg, rowNumber, errors)!,
            Broker = ValidateAndTrim(broker, TariffExcelColumnMap.Broker, rowNumber, errors)!,
            AirSea = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.AirSea), TariffExcelColumnMap.AirSea, rowNumber, errors)!,
            TotalAmountForeignCurrency = totalAmountForeignCurrency,
            TotalAmountTWD = totalAmountTwd,
            DeclarationAmountTWD = ValidateAndTrim(GetCellValue(values, columnMap, TariffExcelColumnMap.DeclarationAmountTWD), TariffExcelColumnMap.DeclarationAmountTWD, rowNumber, errors)!,
            CreateDate = createDate,
            ImportBatchId = importBatchId,
            ImportFileName = ValidateAndTrim(importFileName, nameof(TariffData.ImportFileName), rowNumber, errors, maxLengthOverride: 255)!
        };
    }

    public static void ApplyImportRow(TariffData entity, TariffData source)
    {
        entity.MAWB = source.MAWB;
        entity.HAWB = source.HAWB;
        entity.ImportDate = source.ImportDate;
        entity.DeclarationDate = source.DeclarationDate;
        entity.ReleaseDate = source.ReleaseDate;
        entity.LineNo = source.LineNo;
        entity.PartNumber = source.PartNumber;
        entity.PONumber = source.PONumber;
        entity.DescriptionOfGoods = source.DescriptionOfGoods;
        entity.Quantity = source.Quantity;
        entity.UOM = source.UOM;
        entity.NetWeightKg = source.NetWeightKg;
        entity.UnitValue = source.UnitValue;
        entity.HTSNumber = source.HTSNumber;
        entity.COO = source.COO;
        entity.DutyRate = source.DutyRate;
        entity.DutyTreatment = source.DutyTreatment;
        entity.PermitNo1 = source.PermitNo1;
        entity.PermitItem1 = source.PermitItem1;
        entity.PermitNo2 = source.PermitNo2;
        entity.PermitItem2 = source.PermitItem2;
        entity.PermitNo3 = source.PermitNo3;
        entity.PermitItem3 = source.PermitItem3;
        entity.EntryNumber = source.EntryNumber;
        entity.Type = source.Type;
        entity.Mode = source.Mode;
        entity.PortOfDeparture = source.PortOfDeparture;
        entity.FlightNo = source.FlightNo;
        entity.Shipper = source.Shipper;
        entity.TermsOfTrade = source.TermsOfTrade;
        entity.Currency = source.Currency;
        entity.ExchangeRate = source.ExchangeRate;
        entity.CIFValue = source.CIFValue;
        entity.FreightCharge = source.FreightCharge;
        entity.TotalPieces = source.TotalPieces;
        entity.GrossWeightKg = source.GrossWeightKg;
        entity.Broker = source.Broker;
        entity.AirSea = source.AirSea;
        entity.TotalAmountForeignCurrency = source.TotalAmountForeignCurrency;
        entity.TotalAmountTWD = source.TotalAmountTWD;
        entity.DeclarationAmountTWD = source.DeclarationAmountTWD;
        entity.CreateDate = source.CreateDate;
        entity.ImportBatchId = source.ImportBatchId;
        entity.ImportFileName = source.ImportFileName;
    }

    public static string? GetCellValue(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columnMap,
        string propertyName)
    {
        if (!columnMap.TryGetValue(propertyName, out var index) || index >= values.Count)
        {
            return null;
        }

        return NormalizeCellText(values[index]);
    }

    public static string NormalizeCellText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    public static string FormatCellValue(object? value)
    {
        if (value is null or DBNull)
        {
            return string.Empty;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
        }

        if (value is double number && number is > 0 and < 100000)
        {
            try
            {
                return DateTime.FromOADate(number).ToString("yyyy/MM/dd", CultureInfo.InvariantCulture);
            }
            catch (ArgumentException)
            {
            }
        }

        return NormalizeCellText(value.ToString());
    }

    public static void ThrowIfErrors(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("；", errors.Take(20))
                + (errors.Count > 20 ? $"…等 {errors.Count} 項錯誤" : string.Empty));
        }
    }

    private static bool ContainsAnyKeyword(string fileNameUpper, IReadOnlyList<string> keywords)
    {
        foreach (var keyword in keywords)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                continue;
            }

            if (fileNameUpper.Contains(keyword.ToUpperInvariant(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? RequireCellValue(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columnMap,
        string propertyName,
        int rowNumber,
        List<string> errors)
    {
        var value = GetCellValue(values, columnMap, propertyName);
        if (string.IsNullOrEmpty(value))
        {
            errors.Add($"第 {rowNumber} 列缺少必填欄位 {TariffExcelColumnMap.GetDisplayName(propertyName)}");
        }

        return value;
    }

    private static string? ValidateOptionalTrim(
        string? value,
        string propertyName,
        int rowNumber,
        List<string> errors)
    {
        if (string.IsNullOrEmpty(NormalizeCellText(value)))
        {
            return null;
        }

        return ValidateAndTrim(value, propertyName, rowNumber, errors, required: false);
    }

    private static string? ValidateAndTrim(
        string? value,
        string propertyName,
        int rowNumber,
        List<string> errors,
        bool required = true,
        int? maxLengthOverride = null)
    {
        var normalized = NormalizeCellText(value);
        if (string.IsNullOrEmpty(normalized))
        {
            if (required)
            {
                errors.Add($"第 {rowNumber} 列缺少必填欄位 {TariffExcelColumnMap.GetDisplayName(propertyName)}");
            }

            return required ? string.Empty : null;
        }

        var maxLength = maxLengthOverride ?? GetMaxLength(propertyName);
        if (normalized.Length > maxLength)
        {
            errors.Add($"第 {rowNumber} 列 {TariffExcelColumnMap.GetDisplayName(propertyName)} 超過 {maxLength} 字元");
        }

        return normalized;
    }

    private static int GetMaxLength(string propertyName) =>
        MaxLengths.TryGetValue(propertyName, out var maxLength) ? maxLength : 255;

    private static DateOnly ParseRequiredDateOnly(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columnMap,
        string propertyName,
        int rowNumber,
        List<string> errors)
    {
        var text = GetCellValue(values, columnMap, propertyName);
        if (string.IsNullOrEmpty(text))
        {
            errors.Add($"第 {rowNumber} 列缺少必填欄位 {TariffExcelColumnMap.GetDisplayName(propertyName)}");
            return default;
        }

        if (TryParseStrictDateOnly(text, out var parsed))
        {
            return parsed;
        }

        errors.Add($"第 {rowNumber} 列 {TariffExcelColumnMap.GetDisplayName(propertyName)} 日期格式須為 YYYY/MM/DD");
        return default;
    }

    private static decimal? ParseRequiredDecimal(
        IReadOnlyList<string> values,
        IReadOnlyDictionary<string, int> columnMap,
        string propertyName,
        int rowNumber,
        List<string> errors)
    {
        var text = GetCellValue(values, columnMap, propertyName);
        if (string.IsNullOrEmpty(text))
        {
            errors.Add($"第 {rowNumber} 列缺少必填欄位 {TariffExcelColumnMap.GetDisplayName(propertyName)}");
            return null;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        errors.Add($"第 {rowNumber} 列 {TariffExcelColumnMap.GetDisplayName(propertyName)} 格式不正確");
        return null;
    }

    private static bool TryParseStrictDateOnly(string text, out DateOnly parsed)
    {
        if (DateOnly.TryParseExact(text, "yyyy/MM/dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            || DateOnly.TryParseExact(text, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            || DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return true;
        }

        if (Regex.IsMatch(text, @"^\d{4}/\d{1,2}/\d{1,2}$")
            && DateTime.TryParseExact(text, "yyyy/M/d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime))
        {
            parsed = DateOnly.FromDateTime(dateTime.Date);
            return true;
        }

        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var oaDate)
            && oaDate is > 0 and < 100000)
        {
            try
            {
                parsed = DateOnly.FromDateTime(DateTime.FromOADate(oaDate));
                return true;
            }
            catch (ArgumentException)
            {
            }
        }

        parsed = default;
        return false;
    }
}
