using System.Text.Json;
using ClosedXML.Excel;
using ICPFileGenerator.Models;
using ICPFileGenerator.Services;

internal static class PickupNoticeChecks
{
    private const string FirstInvoice = "010121647400";
    private const string SecondInvoice = "010114246101";
    private const string FullAddress = "Taiwan 710051 Tainan City TAINAN CITY No. 85, Yongke 1st Rd., Yongkang Dist.";
    private static readonly DateTime Stamp = new(2026, 10, 1);

    public static void Run(string outputDirectory)
    {
        var fixtureDirectory = Path.Combine(outputDirectory, "pickup-regression");
        Directory.CreateDirectory(fixtureDirectory);
        var fixturePath = Path.Combine(fixtureDirectory, "seven-source-rows.xlsx");
        CreateFixture(fixturePath);
        var rows = ShippingAdviceSheetReader.Read(fixturePath);
        Check(rows.Count == 7, "reader preserves all seven item rows, including worksheet rows 2 and 3");
        Check(rows[0].InvoiceNo == FirstInvoice && rows[0].ColumnK == "RMA1" && rows[1].ColumnK == "RMA2",
            "reader retains the first invoice and its source order");
        Check(rows.All(row => row.ShipToAddress == FullAddress), "reader uses the full Ship-to Party Address");
        Check(rows.All(row => row.Customer == "Fixture End User" && row.SoldToCompany == "Fixture Sold-to Company" &&
            row.CompanyNameBf == "Fixture Ship-to Company" && row.PortOfDischargeAu == "TPE"),
            "reader keeps End User, Sold-to Party, Ship-to Party and Port of Entry mappings distinct");

        var original = JsonSerializer.Serialize(rows);
        var pickupPath = PickupNoticeExcelGenerator.Generate(rows, fixtureDirectory, Stamp);
        using (var workbook = new XLWorkbook(pickupPath))
        {
            var sheet = workbook.Worksheet(PickupNoticeExcelGenerator.OutputSheetName);
            VerifyFiveCartons(sheet);
            foreach (var expected in new[]
            {
                (Invoice: FirstInvoice, Carton: "1/1", Rma: "RMA1"),
                (Invoice: SecondInvoice, Carton: "1/4", Rma: "RMA3"),
                (Invoice: SecondInvoice, Carton: "2/4", Rma: "RMA5"),
                (Invoice: SecondInvoice, Carton: "3/4", Rma: "RMA6"),
                (Invoice: SecondInvoice, Carton: "4/4", Rma: "RMA7")
            })
            {
                var row = DataRows(sheet).Single(row => row.Cell(1).GetString() == expected.Invoice &&
                    row.Cell(10).GetString() == expected.Carton);
                Check(row.Cell(4).GetString() == expected.Rma && row.Cell(5).GetString() == expected.Rma,
                    $"pickup {expected.Invoice} carton {expected.Carton} uses the first source row for RA and Ref");
            }
        }
        Check(JsonSerializer.Serialize(rows) == original && rows.Count == 7,
            "pickup generation leaves all original item rows and values intact");

        VerifyNormalizedDuplicatesAndEmptyCartons(fixtureDirectory);
    }

    public static void RunUploadedWorkbook(string inputPath, string outputDirectory)
    {
        var rows = ShippingAdviceSheetReader.Read(inputPath);
        var original = JsonSerializer.Serialize(rows);
        var actualDirectory = Path.Combine(outputDirectory, "actual-upload");
        var pickupPath = PickupNoticeExcelGenerator.Generate(rows, actualDirectory, Stamp);
        using (var workbook = new XLWorkbook(pickupPath))
        {
            var sheet = workbook.Worksheet(PickupNoticeExcelGenerator.OutputSheetName);
            VerifyFiveCartons(sheet);
            foreach (var outputRow in DataRows(sheet))
            {
                var invoice = outputRow.Cell(1).GetString();
                var carton = outputRow.Cell(10).GetString().Split('/')[0];
                var source = rows.First(row => row.InvoiceNo.Trim() == invoice &&
                    row.CartonNo.Trim().TrimStart('0') == carton);
                Check(outputRow.Cell(4).GetString() == source.ColumnK && outputRow.Cell(5).GetString() == source.ColumnK,
                    $"actual upload {invoice} carton {carton} retains its first source RA and Ref");
            }
        }
        Check(JsonSerializer.Serialize(rows) == original, "actual pickup generation preserves the full source for case marks");

        FileGenerationService.ResolveCountryNames(rows, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["JP"] = "Japan", ["TW"] = "Taiwan"
        });
        var pdfs = CaseMarkPdfGenerator.GenerateAll(rows, actualDirectory, Stamp);
        Check(pdfs.Count == 2 && pdfs.All(File.Exists), "actual upload produces one case-mark PDF per invoice from all source rows");
        Console.WriteLine($"Actual upload pickup: {pickupPath}");
        foreach (var pdf in pdfs)
        {
            Console.WriteLine($"Actual upload case mark: {pdf}");
        }
    }

    private static void VerifyFiveCartons(IXLWorksheet sheet)
    {
        var outputRows = DataRows(sheet).ToArray();
        Check(outputRows.Length == 5, "pickup contains five carton rows");
        Check(outputRows.Select(row => row.Cell(1).GetString()).Distinct().Count() == 2 &&
            outputRows.Count(row => row.Cell(1).GetString() == FirstInvoice) == 1 &&
            outputRows.Count(row => row.Cell(1).GetString() == SecondInvoice) == 4,
            "pickup retains both invoices with one and four cartons");
        Check(outputRows.All(row => row.Cell(2).GetString() == FullAddress), "pickup exports the complete Ship-to Party Address");
        Check(outputRows.Select(row => $"{row.Cell(1).GetString()}:{row.Cell(10).GetString()}").SequenceEqual(new[]
        {
            $"{SecondInvoice}:1/4", $"{SecondInvoice}:2/4", $"{SecondInvoice}:3/4", $"{SecondInvoice}:4/4", $"{FirstInvoice}:1/1"
        }), "pickup sorts invoices and cartons and normalizes C/NO text");
        Check(outputRows.All(row => row.Cell(10).DataType == XLDataType.Text && row.Cell(10).Style.NumberFormat.Format == "@"),
            "pickup C/NO remains Excel text rather than dates");
    }

    private static void VerifyNormalizedDuplicatesAndEmptyCartons(string outputDirectory)
    {
        var rows = new[]
        {
            new ShippingAdviceRow { InvoiceNo = "INV-A", CartonNo = "1", TotalCartons = "4", ColumnK = "FIRST" },
            new ShippingAdviceRow { InvoiceNo = "INV-A", CartonNo = "001", TotalCartons = "99", ColumnK = "LATER" },
            new ShippingAdviceRow { InvoiceNo = " INV-A ", CartonNo = "0001", TotalCartons = "99", ColumnK = "LATER-TRIMMED" },
            new ShippingAdviceRow { InvoiceNo = "INV-B", CartonNo = "001", TotalCartons = "1", ColumnK = "OTHER-INVOICE" },
            new ShippingAdviceRow { InvoiceNo = "INV-A", CartonNo = "", TotalCartons = "4", ColumnK = "EMPTY-1" },
            new ShippingAdviceRow { InvoiceNo = "INV-A", CartonNo = "  ", TotalCartons = "4", ColumnK = "EMPTY-2" }
        };
        var original = JsonSerializer.Serialize(rows);
        var pickupPath = PickupNoticeExcelGenerator.Generate(rows, Path.Combine(outputDirectory, "normalized-cartons"), Stamp);
        using var workbook = new XLWorkbook(pickupPath);
        var outputRows = DataRows(workbook.Worksheet(PickupNoticeExcelGenerator.OutputSheetName)).ToArray();
        Check(outputRows.Length == 4 && outputRows.Count(row => row.Cell(4).GetString() == "FIRST") == 1 &&
            outputRows.All(row => !row.Cell(4).GetString().StartsWith("LATER", StringComparison.Ordinal)),
            "001 and 1 within a trimmed invoice keep the first source row regardless of total-carton denominator");
        Check(outputRows.Single(row => row.Cell(4).GetString() == "FIRST").Cell(10).GetString() == "1/4",
            "normalized duplicate preserves the first source carton total");
        Check(outputRows.Count(row => row.Cell(4).GetString() == "OTHER-INVOICE") == 1,
            "the same carton number in a different invoice is retained");
        Check(outputRows.Count(row => row.Cell(4).GetString().StartsWith("EMPTY-", StringComparison.Ordinal)) == 2,
            "separate source rows with empty carton identifiers are retained");
        Check(JsonSerializer.Serialize(rows) == original, "deduplication does not mutate or remove source rows");
    }

    private static void CreateFixture(string path)
    {
        var headers = new[]
        {
            "Invoice No.", "Ship-to Party Address", "End User", "Sold-to Party", "RMA#", "SLOC",
            "Delivery No", "Carton No.", "Total Cartons", "Length", "Width", "Height", "Weight",
            "Carton Name", "No Charge Flag", "Ship-to Party", "Port of Entry", "Forwarder", "PO#",
            "SO#", "Customer PO No.", "Country of Origin", "Sold-to Party Address", "Port of Discharge"
        };
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Uploaded data");
        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }
        for (var index = 0; index < 7; index++)
        {
            var firstInvoice = index < 2;
            var values = new[]
            {
                firstInvoice ? FirstInvoice : SecondInvoice, FullAddress, "Fixture End User", "Fixture Sold-to Company",
                $"RMA{index + 1}", "SLOC1", $"DO{index + 1}", index < 4 ? "001" : (index - 2).ToString("D3"),
                firstInvoice ? "1" : "4", "39", "33", "18", "2", "CARTON", firstInvoice ? "X" : "",
                "Fixture Ship-to Company", "TPE", "Fixture Forwarder", "PO1", "SO1", "CUSTOMER-PO1",
                index % 2 == 0 ? "JP" : "TW", "Wrong sold-to address", "WRONG DISCHARGE"
            };
            for (var column = 0; column < values.Length; column++)
            {
                sheet.Cell(index + 2, column + 1).Value = values[column];
            }
        }
        workbook.SaveAs(path);
    }

    private static IEnumerable<IXLRow> DataRows(IXLWorksheet sheet) => sheet.RowsUsed().Skip(1);

    private static void Check(bool success, string description)
    {
        if (!success) throw new Exception("FAIL: " + description);
        Console.WriteLine("PASS: " + description);
    }
}
