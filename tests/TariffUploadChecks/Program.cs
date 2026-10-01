using System.Reflection;
using System.Text.RegularExpressions;
using ICP.Controllers;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Services;

// Pure row-mapping, formatting, and contract checks. No workbook or database is opened.
int count = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}

foreach (var header in new[] { "Broker", " broker ", "報關行", " * 報關行 " })
{
    Check(TariffExcelColumnMap.TryResolveProperty(header, out var property)
        && property == TariffExcelColumnMap.Broker, $"Broker header alias resolves: {header}");
}
Check(TariffCustomsImportRules.RequiredHeaderColumnProperties.Contains(TariffExcelColumnMap.Broker),
    "Broker is a required upload header");
Check(TariffCustomsImportRules.IsRequiredCellColumn(TariffExcelColumnMap.Broker),
    "Broker is a required row value");
Check(TariffExcelColumnMap.IsRequiredColumn(TariffExcelColumnMap.Broker),
    "Template metadata identifies Broker as required");

var missingBrokerHeaders = ColumnMap(ValidCells());
missingBrokerHeaders.Remove(TariffExcelColumnMap.Broker);
var headerErrors = new List<string>();
TariffCustomsImportRules.ValidateRequiredHeaders(missingBrokerHeaders, headerErrors);
TariffCustomsImportRules.ValidateRequiredHeaders(missingBrokerHeaders, headerErrors);
Check(headerErrors.Count == 1 && headerErrors[0].Contains("Broker", StringComparison.Ordinal),
    "Missing Broker header is rejected without duplicate messages");
Check(Rejects(headerErrors), "Header validation errors reject the import");

var firstCells = ValidCells();
firstCells[TariffExcelColumnMap.Broker] = "  LOG KWE  ";
var (firstRow, firstErrors) = Map(firstCells);
var secondCells = ValidCells();
secondCells[TariffExcelColumnMap.Broker] = "  任意報關行 / Custom Broker  ";
var (secondRow, secondErrors) = Map(secondCells, 3);
Check(firstErrors.Count == 0 && secondErrors.Count == 0,
    "Distinct brokers, including an arbitrary name, are accepted in the same upload");
Check(firstRow.Broker == "LOG KWE" && secondRow.Broker == "任意報關行 / Custom Broker",
    "Each row retains its own trimmed Broker cell without a role-name conversion");
Check(firstRow.TotalAmountForeignCurrency == 20m && firstRow.TotalAmountTWD == 640m,
    "Normal numeric calculations survive row-based Broker mapping");

foreach (var emptyBroker in new[] { "", " \t " })
{
    var cells = ValidCells();
    cells[TariffExcelColumnMap.Broker] = emptyBroker;
    var (_, errors) = Map(cells);
    Check(errors.Count == 1 && errors[0].Contains("缺少必填欄位 Broker", StringComparison.Ordinal),
        "An empty or whitespace Broker produces exactly one missing-field error");
    Check(Rejects(errors), "An empty Broker rejects the import");
}
foreach (var length in new[] { 200, 201 })
{
    var cells = ValidCells();
    var broker = new string('關', length);
    cells[TariffExcelColumnMap.Broker] = " " + broker + " ";
    var (row, errors) = Map(cells);
    Check(row.Broker == broker, $"Broker preserves all {length} characters after trimming");
    Check(length == 200 ? errors.Count == 0 : errors.Count == 1
        && errors[0].Contains("Broker", StringComparison.Ordinal)
        && errors[0].Contains("200", StringComparison.Ordinal),
        $"Broker {(length == 200 ? "accepts" : "rejects")} {length} characters");
    Check(Rejects(errors) == (length > 200), $"Broker {length}-character validation controls import rejection");
}

// Reproduce the screenshot: two missing fields in each of Excel rows 2 and 3.
var screenshotErrors = new List<string>();
foreach (var rowNumber in new[] { 2, 3 })
{
    var cells = ValidCells();
    cells[TariffExcelColumnMap.AirSea] = "";
    cells[TariffExcelColumnMap.DeclarationAmountTWD] = "";
    Map(cells, rowNumber, screenshotErrors);
}
Check(screenshotErrors.Count == 4 && screenshotErrors.Distinct(StringComparer.Ordinal).Count() == 4,
    "Rows 2 and 3 contain exactly two distinct raw missing-field errors each");
var screenshotMessage = TariffImportErrorFormatter.Format(screenshotErrors);
Check(screenshotMessage == "【第2列】缺少必填欄位 Air/Sea；缺少必填欄位 Declaration Amount (TWD)\n"
        + "【第3列】缺少必填欄位 Air/Sea；缺少必填欄位 Declaration Amount (TWD)",
    "Screenshot errors render as two ordered rows, each listing its two missing fields once");
Check(Rejects(screenshotErrors, screenshotMessage), "ThrowIfErrors returns the grouped, newline-separated message");

foreach (var property in new[]
{
    TariffExcelColumnMap.ImportDate, TariffExcelColumnMap.DeclarationDate, TariffExcelColumnMap.ReleaseDate,
    TariffExcelColumnMap.Quantity, TariffExcelColumnMap.UnitValue, TariffExcelColumnMap.ExchangeRate
})
{
    var cells = ValidCells();
    cells[property] = " \t ";
    var (_, errors) = Map(cells);
    Check(errors.Count == 1 && errors[0] == $"第 2 列缺少必填欄位 {TariffExcelColumnMap.GetDisplayName(property)}",
        $"Blank {property} produces one missing-field error across presence and parse validation");
    Check(Rejects(errors), $"Blank {property} rejects the import");

    cells[property] = property.EndsWith("Date", StringComparison.Ordinal) ? "2026/02/30" : "not-a-number";
    (_, errors) = Map(cells);
    Check(errors.Count == 1 && errors[0].Contains(TariffExcelColumnMap.GetDisplayName(property), StringComparison.Ordinal)
        && errors[0].Contains("格式", StringComparison.Ordinal),
        $"Invalid {property} still produces its format error");
    Check(Rejects(errors), $"Invalid {property} rejects the import");
}

var mixedErrors = new[]
{
    "第 10 列缺少必填欄位 Air/Sea",
    "標題列缺少必填欄位 Broker",
    "第3列缺少必填欄位 Quantity",
    "第 2 列缺少必填欄位 Import Date",
    "第 3 列缺少必填欄位 Quantity",
    "標題列缺少必填欄位 Broker",
    "第3列缺少必填欄位 Unit Value",
    "Invoice Number INV-001 的 HAWB HOUSE-001 不存在於 ICP 報關資料",
    "第2列缺少必填欄位 Import Date"
};
Check(TariffImportErrorFormatter.Format(mixedErrors) ==
    "標題列缺少必填欄位 Broker\n"
    + "Invoice Number INV-001 的 HAWB HOUSE-001 不存在於 ICP 報關資料\n"
    + "【第2列】缺少必填欄位 Import Date\n"
    + "【第3列】缺少必填欄位 Quantity；缺少必填欄位 Unit Value\n"
    + "【第10列】缺少必填欄位 Air/Sea",
    "Formatter deduplicates mixed row spacing, sorts row numbers numerically, and preserves message order");
Check(TariffImportErrorFormatter.Format([]) == "", "Formatter accepts an empty error list");
Check(!Rejects([]), "An error-free import is not rejected");

var manyMessagesOneRow = Enumerable.Range(1, 25).Select(index => $"第 2 列錯誤 {index}").ToArray();
Check(TariffImportErrorFormatter.Format(manyMessagesOneRow).Split('\n').Length == 1
    && TariffImportErrorFormatter.Format(manyMessagesOneRow).Contains("錯誤 25", StringComparison.Ordinal),
    "All field messages for one source row remain on one line");
var manyRowErrors = Enumerable.Range(2, 23).Reverse()
    .SelectMany(rowNumber => new[] { $"第 {rowNumber} 列缺少必填欄位 Air/Sea", $"第{rowNumber}列缺少必填欄位 Air/Sea" })
    .ToArray();
var allLines = TariffImportErrorFormatter.Format(manyRowErrors).Split('\n');
Check(allLines.Length == 23
    && allLines.Select((line, index) => line == $"【第{index + 2}列】缺少必填欄位 Air/Sea").All(value => value),
    "All 23 row groups remain visible in numeric row order without duplicate messages");

var originalCreated = new DateTime(2025, 1, 2, 3, 4, 5);
var existing = new TariffData
{
    InvoiceNumber = "INV-001",
    Broker = "Previous Broker",
    CreateUser = @"DOMAIN\original.creator",
    CreateTime = originalCreated,
    LOGRemarks = "Key User maintained remark",
    DeclarationFile = "existing.pdf",
    Cost = "existing.xlsx"
};
secondRow.CreateUser = "workbook-supplied-user";
secondRow.CreateTime = new DateTime(2026, 10, 1);
secondRow.LOGRemarks = "workbook-supplied remark";
TariffCustomsImportRules.ApplyImportRow(existing, secondRow);
Check(existing.Broker == secondRow.Broker, "Reimport updates the existing record from its row Broker cell");
Check(existing.CreateUser == @"DOMAIN\original.creator" && existing.CreateTime == originalCreated,
    "Reimport mapping preserves the original creator account and timestamp");
Check(existing.LOGRemarks == "Key User maintained remark", "Reimport preserves independently maintained LOG remarks");
Check(existing.DeclarationFile == "existing.pdf" && existing.Cost == "existing.xlsx",
    "Reimport with absent attachments preserves existing attachment paths");

foreach (var (type, methodName) in new[]
{
    (typeof(TariffCustomsImportRules), nameof(TariffCustomsImportRules.MapRow)),
    (typeof(TariffDataImportService), nameof(TariffDataImportService.ImportCustomsDataAsync)),
    (typeof(TariffDataController), nameof(TariffDataController.UploadCustomsData))
})
{
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
        .Where(method => method.Name == methodName).ToArray();
    Check(methods.Length == 1 && methods[0].GetParameters().All(parameter =>
        parameter.Name is not null && !parameter.Name.Contains("broker", StringComparison.OrdinalIgnoreCase)),
        $"{type.Name}.{methodName} accepts no form or role Broker override");
}

Console.WriteLine($"{count} checks passed; no database connections or workbook files were used.");

static bool Rejects(List<string> errors, string? expectedMessage = null)
{
    try
    {
        TariffCustomsImportRules.ThrowIfErrors(errors);
        return false;
    }
    catch (InvalidOperationException exception)
    {
        if (expectedMessage is not null && exception.Message != expectedMessage)
            throw new Exception("Unexpected validation message: " + exception.Message);
        return true;
    }
}

static Dictionary<string, int> ColumnMap(Dictionary<string, string> cells) => cells.Keys
    .Select((property, index) => (property, index))
    .ToDictionary(entry => entry.property, entry => entry.index, StringComparer.OrdinalIgnoreCase);

static (TariffData Row, List<string> Errors) Map(Dictionary<string, string> cells, int rowNumber = 2,
    List<string>? errors = null)
{
    errors ??= [];
    var row = TariffCustomsImportRules.MapRow(cells.Values.ToArray(), ColumnMap(cells),
        "customs-data.xlsx", Guid.Empty, new DateOnly(2026, 10, 1), rowNumber, errors);
    return (row, errors);
}

static Dictionary<string, string> ValidCells() => new()
{
    [TariffExcelColumnMap.MAWB] = "MASTER-001",
    [TariffExcelColumnMap.HAWB] = "HOUSE-001",
    [TariffExcelColumnMap.ImportDate] = "2026/10/01",
    [TariffExcelColumnMap.DeclarationDate] = "2026/10/01",
    [TariffExcelColumnMap.ReleaseDate] = "2026/10/01",
    [TariffExcelColumnMap.LineNo] = "1",
    [TariffExcelColumnMap.PartNumber] = "PART-001",
    [TariffExcelColumnMap.InvoiceNumber] = "INV-001",
    [TariffExcelColumnMap.PONumber] = "PO-001",
    [TariffExcelColumnMap.DescriptionOfGoods] = "Test goods",
    [TariffExcelColumnMap.Quantity] = "2",
    [TariffExcelColumnMap.UOM] = "PCS",
    [TariffExcelColumnMap.NetWeightKg] = "1",
    [TariffExcelColumnMap.UnitValue] = "10",
    [TariffExcelColumnMap.HTSNumber] = "1234567890",
    [TariffExcelColumnMap.COO] = "TW",
    [TariffExcelColumnMap.DutyRate] = "0",
    [TariffExcelColumnMap.DutyTreatment] = "NORMAL",
    [TariffExcelColumnMap.EntryNumber] = "ENTRY-001",
    [TariffExcelColumnMap.Type] = "IMPORT",
    [TariffExcelColumnMap.Mode] = "AIR",
    [TariffExcelColumnMap.PortOfDeparture] = "TPE",
    [TariffExcelColumnMap.FlightNo] = "FLIGHT-001",
    [TariffExcelColumnMap.Shipper] = "Test shipper",
    [TariffExcelColumnMap.TermsOfTrade] = "CIF",
    [TariffExcelColumnMap.Currency] = "USD",
    [TariffExcelColumnMap.ExchangeRate] = "32",
    [TariffExcelColumnMap.CIFValue] = "640",
    [TariffExcelColumnMap.FreightCharge] = "0",
    [TariffExcelColumnMap.TotalPieces] = "1",
    [TariffExcelColumnMap.GrossWeightKg] = "1",
    [TariffExcelColumnMap.Broker] = "LOG KWE",
    [TariffExcelColumnMap.AirSea] = "AIR",
    [TariffExcelColumnMap.DeclarationAmountTWD] = "640"
};
