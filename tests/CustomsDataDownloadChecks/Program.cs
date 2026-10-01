using System.Text.Json;
using ICP.Data;
using ICP.Helpers;
using ICP.Models.CustomsDataDownload;
using ICP.Models.Icp;
using Microsoft.EntityFrameworkCore;

var count = 0;
void Check(bool passed, string name)
{
    if (!passed) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    count++;
}

var options = JsonSerializer.Deserialize<CustomsDataDownloadTableFieldsOptions>(
    File.ReadAllText("ICP/Config/customs-data-download-table-fields.json"))!;
var fields = options.List!.Fields.Select(field => new CustomsDataDownloadTableFieldMetadata
{
    FieldName = field.FieldName,
    Visible = field.Visible ?? true,
    Searchable = field.Searchable ?? false,
    FilterType = field.FilterType ?? "Checkbox"
}).Where(field => field.Visible).ToList();
var config = new CustomsDataDownloadTablePageConfig
{
    Fields = fields,
    InitialSort = options.InitialSort
};
var sort = config.ResolveInitialSortColumns();
Check(sort.Count == 5 && new[] { "CreatedUtc", "Mawb", "Hawb", "InvoiceNo", "InvoiceSeq" }
        .Select((name, index) => fields[(int)sort[index][0]].FieldName == name).All(value => value),
    "Five configured sort keys resolve in order");
Check((string)sort[0][1] == "desc" && sort.Skip(1).All(value => (string)value[1] == "asc"),
    "Date descends and remaining sort keys ascend");
var removed = new[] { "RunId", "FileCode", "SourceFileName", "SourceFileDate", "OrderPriority", "NcdrNo",
    "EndUserCode", "EndUser", "MachineNo", "MachineType", "ShipReason", "DeliveryNo",
    "DeliveryLineNo", "ShipToPartyCode", "SoldToPartyCode", "SoldToParty", "WbsElement" };
Check(removed.All(name => fields.All(field => field.FieldName != name)),
    "Removed columns are absent from visible output and Excel metadata");
Check(fields.First(field => field.FieldName == "InvoiceDate").FilterType == "Checkbox"
    && fields.First(field => field.FieldName == "Etd").FilterType == "DateRange"
    && fields.First(field => field.FieldName == "Eta").FilterType == "DateRange",
    "Invoice date stays checkbox while ETD and ETA use date ranges");

var moment = new DateTime(2026, 10, 1, 8, 0, 0);
var rows = new[]
{
    new StgRawShippingAdvice { CreatedUtc = moment.AddDays(-1), Mawb = "M1", Hawb = "H1", InvoiceNo = "I1", InvoiceSeq = 1 },
    new StgRawShippingAdvice { CreatedUtc = moment, Mawb = "M10", Hawb = "H1", InvoiceNo = "I1", InvoiceSeq = 1 },
    new StgRawShippingAdvice { CreatedUtc = moment, Mawb = "M2", Hawb = "H10", InvoiceNo = "I1", InvoiceSeq = 1 },
    new StgRawShippingAdvice { CreatedUtc = moment, Mawb = "M2", Hawb = "H2", InvoiceNo = "I10", InvoiceSeq = 1 },
    new StgRawShippingAdvice { CreatedUtc = moment, Mawb = "M2", Hawb = "H2", InvoiceNo = "I2", InvoiceSeq = 10 },
    new StgRawShippingAdvice { CreatedUtc = moment, Mawb = "M2", Hawb = "H2", InvoiceNo = "I2", InvoiceSeq = 2 }
};
var ordered = CustomsDataDownloadSortHelper.Apply(rows, config);
Check(ordered.SequenceEqual(new[] { rows[5], rows[4], rows[3], rows[2], rows[1], rows[0] }),
    "Natural strings and numeric invoice sequence sort after CreatedUtc");
Check(CustomsDataDownloadTableViewHelper.FormatCellValue(new() { Qty = 1.2m }, "Qty") == "1.200"
    && CustomsDataDownloadTableViewHelper.FormatCellValue(new() { Price = 1.2, Amount = 12.3 }, "Price") == "1.20"
    && CustomsDataDownloadTableViewHelper.FormatCellValue(new() { Amount = 12.3 }, "Amount") == "12.30"
    && CustomsDataDownloadTableViewHelper.FormatCellValue(new() { GrossWeight = 1.2m }, "GrossWeight") == "1.2000"
    && CustomsDataDownloadTableViewHelper.FormatCellValue(new() { NetWeightOfTheItem = 1.2 }, "NetWeightOfTheItem") == "1.2000",
    "Requested decimal formats apply to both table and Excel export");

var dateRows = new[]
{
    new StgRawShippingAdvice { Etd = "2026-09-30", Eta = "2026-10-01", InvoiceDate = "X", Hazmat = "Y" },
    new StgRawShippingAdvice { Etd = "2026/10/01 14:30", Eta = "2026/10/02", InvoiceDate = null, Hazmat = " " },
    new StgRawShippingAdvice { Etd = "2026-10-02", Eta = "2026-10-03", InvoiceDate = "", Hazmat = null }
};
var ranged = CustomsDataDownloadQueryFilterApplier.ApplyFilters(dateRows.AsQueryable(), new()
{
    DateFrom = new() { ["Etd"] = "2026-10-01" },
    DateTo = new() { ["Etd"] = "2026-10-01" }
}, fields).ToList();
Check(ranged.Count == 1 && ReferenceEquals(ranged[0], dateRows[1]),
    "ETD range includes the whole end date and yyyy/MM/dd source values");
var blank = CustomsDataDownloadQueryFilterApplier.BlankFilterValue;
var filtered = CustomsDataDownloadQueryFilterApplier.ApplyFilters(dateRows.AsQueryable(), new()
{
    Checkbox = new() { ["InvoiceDate"] = [blank], ["Hazmat"] = [blank] }
}, fields).ToList();
Check(filtered.SequenceEqual(dateRows.Skip(1)), "Blank checkbox matches null, empty, and whitespace");
filtered = CustomsDataDownloadQueryFilterApplier.ApplyFilters(dateRows.AsQueryable(), new()
{
    Checkbox = new() { ["InvoiceDate"] = ["X", blank] }
}, fields).ToList();
Check(filtered.SequenceEqual(dateRows), "Invoice date checkbox combines X and blank");

using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer("Server=localhost;Database=NoConnection;Integrated Security=true;TrustServerCertificate=true").Options);
var sql = CustomsDataDownloadQueryFilterApplier.ApplyFilters(db.StgRawShippingAdvice, new()
{
    DateFrom = new() { ["Etd"] = "2026-10-01" },
    DateTo = new() { ["Eta"] = "2026-10-03" },
    Checkbox = new() { ["InvoiceDate"] = ["X", blank], ["Hazmat"] = [blank] }
}, fields).ToQueryString();
Check(sql.Contains("REPLACE", StringComparison.OrdinalIgnoreCase)
    && sql.Contains("INVOICE_DATE", StringComparison.Ordinal)
    && sql.Contains("HAZMAT", StringComparison.Ordinal),
    "SQL Server translates text date ranges and both blank checkbox filters");

Console.WriteLine($"{count} checks passed without a database connection.");
