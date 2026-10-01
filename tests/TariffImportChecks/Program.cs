using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.Icp;
using ICP.Models.Tariff;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

int count = 0;
void Check(bool result, string name)
{
    if (!result) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}

var descriptionAttribute = typeof(TariffData)
    .GetProperty(nameof(TariffData.DescriptionOfGoods))!
    .GetCustomAttribute<MaxLengthAttribute>();
Check(descriptionAttribute?.Length == 500, "Description model annotation permits 500 characters");

// Inspect model metadata only. No database connection is opened.
using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=localhost;Database=TariffImportChecks;Integrated Security=true;TrustServerCertificate=true")
    .Options);
var descriptionProperty = db.Model.FindEntityType(typeof(TariffData))!
    .FindProperty(nameof(TariffData.DescriptionOfGoods))!;
Check(descriptionProperty.GetMaxLength() == 500, "Description EF mapping permits 500 characters");
Check(!descriptionProperty.IsNullable, "Description EF mapping remains required");

var remarksAttribute = typeof(TariffData).GetProperty(nameof(TariffData.LOGRemarks))!
    .GetCustomAttribute<MaxLengthAttribute>();
var remarksProperty = db.Model.FindEntityType(typeof(TariffData))!
    .FindProperty(nameof(TariffData.LOGRemarks))!;
Check(remarksAttribute?.Length == 100 && remarksProperty.GetMaxLength() == 100,
    "LOG remarks model annotation and EF mapping permit 100 characters");
Check(remarksProperty.IsNullable, "LOG remarks remain optional");

foreach (var length in new[] { 200, 201, 500 })
{
    var description = new string('A', length);
    var (row, errors) = MapDescription(description);
    Check(errors.Count == 0, $"Import accepts ASCII description of {length} characters: {string.Join("; ", errors)}");
    Check(row.DescriptionOfGoods == description, $"Import retains all {length} ASCII characters");
    TariffCustomsImportRules.ThrowIfErrors(errors);
}

var chineseDescription = new string('\u8ca8', 500);
var (chineseRow, chineseErrors) = MapDescription(chineseDescription);
Check(chineseErrors.Count == 0, "Import accepts 500 Chinese characters");
Check(chineseRow.DescriptionOfGoods == chineseDescription, "Import retains all 500 Chinese characters");
TariffCustomsImportRules.ThrowIfErrors(chineseErrors);

foreach (var description in new[] { new string('A', 501), new string('\u8ca8', 501) })
{
    var (row, errors) = MapDescription(description);
    Check(errors.Count == 1
        && errors[0].Contains("Description Of Goods", StringComparison.Ordinal)
        && errors[0].Contains("500", StringComparison.Ordinal),
        $"Import reports the 500-character limit for 501 {(description[0] == 'A' ? "ASCII" : "Chinese")} characters");
    Check(row.DescriptionOfGoods == description, "Overlength description is reported without silently truncating it");
    var rejected = false;
    try
    {
        TariffCustomsImportRules.ThrowIfErrors(errors);
    }
    catch (InvalidOperationException)
    {
        rejected = true;
    }
    Check(rejected, "Import validation rejects the overlength row before persistence");
}

var existingRow = new TariffData { DescriptionOfGoods = "Previous description", LOGRemarks = "Key User remark" };
TariffCustomsImportRules.ApplyImportRow(existingRow, chineseRow);
Check(existingRow.DescriptionOfGoods == chineseDescription, "Updating an existing import row preserves the complete 500-character description");
Check(existingRow.LOGRemarks == "Key User remark", "Broker reimport preserves LOG remarks");

var sortFields = new[] { "ImportDate", "MAWB", "HAWB", "InvoiceNumber", "LineNo" }
    .Select(name => new TariffTableFieldMetadata { FieldName = name }).ToList();
var sortConfig = new TariffTablePageConfig
{
    Fields = sortFields,
    InitialSort = new TariffTableInitialSort
    {
        FieldName = "ImportDate", Direction = "desc",
        ThenBy = sortFields.Skip(1).Select(field => new TariffTableInitialSort { FieldName = field.FieldName }).ToList()
    }
};
var resolved = sortConfig.ResolveInitialSortColumns();
Check(JsonSerializer.Serialize(resolved) == "[[0,\"desc\"],[1,\"asc\"],[2,\"asc\"],[3,\"asc\"],[4,\"asc\"]]",
    "Five sorting priorities resolve to DataTables column tuples");

var date = new DateOnly(2026, 10, 2);
var sortRows = new List<TariffData>
{
    new() { Id = 6, ImportDate = date.AddDays(-1), MAWB = "M1", HAWB = "H1", InvoiceNumber = "I1", LineNo = "1" },
    new() { Id = 5, ImportDate = date, MAWB = "M10", HAWB = "H1", InvoiceNumber = "I1", LineNo = "1" },
    new() { Id = 4, ImportDate = date, MAWB = "M2", HAWB = "H10", InvoiceNumber = "I1", LineNo = "1" },
    new() { Id = 3, ImportDate = date, MAWB = "M2", HAWB = "H2", InvoiceNumber = "I10", LineNo = "1" },
    new() { Id = 2, ImportDate = date, MAWB = "M2", HAWB = "H2", InvoiceNumber = "I2", LineNo = "10" },
    new() { Id = 1, ImportDate = date, MAWB = "M2", HAWB = "H2", InvoiceNumber = "I2", LineNo = "2" }
};
Check(TariffTableSortHelper.Apply(sortRows, sortConfig).Select(row => row.Id).SequenceEqual(new long[] { 1, 2, 3, 4, 5, 6 }),
    "Server sorting applies date descending then natural MAWB, HAWB, invoice and line priorities");
Check(sortRows[0].Id == 6, "Server sorting does not mutate the input list");

var legacyOptions = JsonSerializer.Deserialize<TariffTableFieldsOptions>(
    "{\"initialSort\":{\"fieldName\":\"MAWB\",\"direction\":\"DESC\"}}")!;
var legacyConfig = new TariffTablePageConfig { Fields = sortFields, InitialSort = legacyOptions.InitialSort };
Check(legacyConfig.ResolveInitialSortColumnIndex() == 1
    && JsonSerializer.Serialize(legacyConfig.ResolveInitialSortColumns()) == "[[1,\"desc\"]]",
    "Legacy single-sort configuration and column resolver remain compatible");
Check(TariffTableSortHelper.Apply(sortRows, legacyConfig)[0].MAWB == "M10", "Natural ordering supports descending direction");

var hiddenConfig = new TariffTablePageConfig
{
    Fields = [new() { FieldName = "ImportDate", Visible = false }, new() { FieldName = "MAWB" }, new() { FieldName = "HAWB" }],
    InitialSort = new()
    {
        FieldName = "ImportDate",
        ThenBy = [new() { FieldName = "Missing" }, new() { FieldName = "MAWB", Direction = " DESC " },
            new() { FieldName = "mawb" }, new() { FieldName = "HAWB", Direction = "invalid",
                ThenBy = [new() { FieldName = "LineNo" }] }]
    }
};
Check(JsonSerializer.Serialize(hiddenConfig.ResolveInitialSortColumns()) == "[[0,\"desc\"],[1,\"asc\"]]",
    "Sort resolution skips hidden, missing and duplicate fields, normalizes direction, and uses direct thenBy only");
var noSortConfig = new TariffTablePageConfig { Fields = sortFields };
Check(noSortConfig.ResolveInitialSortColumns().Count == 0
    && TariffTableSortHelper.Apply(sortRows, noSortConfig).Select(row => row.Id).SequenceEqual(sortRows.Select(row => row.Id)),
    "Missing sort configuration preserves input order");

var naturalConfig = new TariffTablePageConfig
{
    Fields = [new() { FieldName = "MAWB" }],
    InitialSort = new() { FieldName = "MAWB" }
};
var naturalRows = new[] { "A10B1", "A2B10", "a2b2", "A002B02", "A2B2" }
    .Select((value, index) => new TariffData { Id = index, MAWB = value }).ToList();
Check(TariffTableSortHelper.Apply(naturalRows, naturalConfig).Select(row => row.Id).SequenceEqual(new long[] { 2, 3, 4, 1, 0 }),
    "Natural strings compare each numeric run, ignore case and leading zeros, and retain stable ties");
var largeRows = new[] { "A9007199254740993", "A9007199254740992", "A1000000000000000000000000000000", "A999999999999999999999999999999" }
    .Select((value, index) => new TariffData { Id = index, MAWB = value }).ToList();
Check(TariffTableSortHelper.Apply(largeRows, naturalConfig).Select(row => row.Id).SequenceEqual(new long[] { 1, 0, 3, 2 }),
    "Natural sorting preserves precision beyond double and decimal numeric ranges");
var zeroRows = new[] { "A000", "a0", "A00", "A1" }
    .Select((value, index) => new TariffData { Id = index, MAWB = value }).ToList();
Check(TariffTableSortHelper.Apply(zeroRows, naturalConfig).Select(row => row.Id).SequenceEqual(new long[] { 0, 1, 2, 3 }),
    "All-zero numeric runs compare equal and remain stable");

var sourceRoot = new DirectoryInfo(Directory.GetCurrentDirectory());
while (sourceRoot is not null && !File.Exists(Path.Combine(sourceRoot.FullName, "ICP", "Config", "tariff-table-fields.json")))
{
    sourceRoot = sourceRoot.Parent;
}
if (sourceRoot is null) throw new InvalidOperationException("Run these checks from the repository or one of its subdirectories.");
foreach (var fileName in new[] { "tariff-table-fields.json", "tariff-table-fields.example.json" })
{
    var bound = new ConfigurationBuilder().SetBasePath(Path.Combine(sourceRoot.FullName, "ICP", "Config"))
        .AddJsonFile(fileName, optional: false).Build().Get<TariffTableFieldsOptions>()!;
    var boundConfig = new TariffTablePageConfig
    {
        InitialSort = bound.InitialSort,
        Fields = bound.List!.Fields.Select(field => new TariffTableFieldMetadata
        {
            FieldName = field.FieldName, Visible = field.Visible ?? true
        }).ToList()
    };
    var visibleFields = boundConfig.Fields.Where(field => field.Visible).ToList();
    var boundSort = boundConfig.ResolveInitialSortColumns()
        .Select(column => $"{visibleFields[(int)column[0]].FieldName}:{column[1]}");
    Check(boundSort.SequenceEqual(new[] { "ImportDate:desc", "MAWB:asc", "HAWB:asc", "InvoiceNumber:asc", "LineNo:asc" }),
        $"Real ConfigurationBuilder binding resolves all five priorities in {fileName}");
}

var pagedRows = Enumerable.Range(1, 25).Reverse().Select(index => new TariffData
{
    Id = index, ImportDate = date, MAWB = "M1", HAWB = "H1", InvoiceNumber = "I1", LineNo = index.ToString()
}).ToList();
Check(TariffTableSortHelper.Apply(pagedRows, sortConfig).Skip(10).Take(10).Select(row => row.Id)
    .SequenceEqual(Enumerable.Range(11, 10).Select(index => (long)index)),
    "Global natural ordering yields the correct second page across 25 rows");

Console.WriteLine($"{count} checks passed.");

static (TariffData Row, List<string> Errors) MapDescription(string description)
{
    var cells = new Dictionary<string, string>
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
        [TariffExcelColumnMap.DescriptionOfGoods] = description,
        [TariffExcelColumnMap.Quantity] = "1",
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
        [TariffExcelColumnMap.CIFValue] = "320",
        [TariffExcelColumnMap.FreightCharge] = "0",
        [TariffExcelColumnMap.TotalPieces] = "1",
        [TariffExcelColumnMap.GrossWeightKg] = "1",
        [TariffExcelColumnMap.Broker] = "LOG KWE",
        [TariffExcelColumnMap.AirSea] = "AIR",
        [TariffExcelColumnMap.DeclarationAmountTWD] = "320"
    };
    var entries = cells.ToArray();
    var columnMap = entries.Select((entry, index) => (entry.Key, Index: index))
        .ToDictionary(entry => entry.Key, entry => entry.Index, StringComparer.OrdinalIgnoreCase);
    var errors = new List<string>();
    var row = TariffCustomsImportRules.MapRow(
        entries.Select(entry => entry.Value).ToArray(), columnMap,
        "customs-data.xlsx", Guid.Empty, new DateOnly(2026, 10, 1), 2, errors);
    return (row, errors);
}
