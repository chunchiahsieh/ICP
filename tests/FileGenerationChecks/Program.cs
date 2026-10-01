using ClosedXML.Excel;
using ICPFileGenerator.Models;
using ICPFileGenerator.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

if (args.Length is < 2 or > 3)
{
    throw new ArgumentException("Pass the 20260916 workbook path, a temporary output folder, and optionally the actual uploaded workbook path.");
}

Directory.CreateDirectory(args[1]);
var inputPath = Path.Combine(args[1], "first-sheet-input.xlsx");
using (var workbook = new XLWorkbook(args[0]))
{
    // The sample contains multiple example sheets; put real data first with an arbitrary name.
    var source = workbook.Worksheet("to BE Shipping advice Report");
    source.Name = "Uploaded data";
    source.Position = 1;
    workbook.Worksheets.Add("to BE Shipping advice Report").Cell(1, 1).Value = "Do not read this sheet";
    workbook.SaveAs(inputPath);
}

var rows = ShippingAdviceSheetReader.Read(inputPath);
var first = rows.First();
Check(first.InvoiceNo == "080254321400", "reads the first worksheet regardless of its name or later named sheets");
Check(first.TotalCartons == "1", "Total Cartons resolves to BN in the new workbook");
Check(first.ForwarderBl == "UPS JAPAN", "Forwarder resolves to BM");
Check(first.CartonNo == "001", "Carton No. resolves to BR");
Check(first.Weight == "2" && first.Length == "39" && first.Width == "33" && first.Height == "18",
    "weight and dimensions use their titled columns");
Check(first.IsNoCharge, "No Charge Flag resolves to AK");
Check(first.CountryOfOriginCode == "CN", "Country of Origin resolves to U");

var invalidFirstPath = Path.Combine(args[1], "invalid-first-sheet.xlsx");
using (var workbook = new XLWorkbook(inputPath))
{
    workbook.Worksheets.Add("Instructions").Position = 1;
    workbook.SaveAs(invalidFirstPath);
}
try
{
    ShippingAdviceSheetReader.Read(invalidFirstPath);
    throw new Exception("Expected rejection of the first worksheet without required columns.");
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Required Shipping advice column"))
{
    Console.WriteLine("PASS: rejects an invalid first worksheet instead of reading later valid data");
}

var mixed = new[]
{
    new ShippingAdviceRow { InvoiceNo = "TEST001", CartonNo = "1", TotalCartons = "1", CountryOfOriginCode = "JP" },
    new ShippingAdviceRow { InvoiceNo = "TEST001", CartonNo = "1", TotalCartons = "1", CountryOfOriginCode = "TW" },
    new ShippingAdviceRow { InvoiceNo = "TEST001", CartonNo = "1", TotalCartons = "1", CountryOfOriginCode = "JP" }
};
var countries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["JP"] = "Japan", ["TW"] = "Taiwan, Province of China"
};
FileGenerationService.ResolveCountryNames(mixed, countries);
Check(mixed[1].CountryOfOriginName == "Taiwan", "case mark uses Taiwan display name");
var pdfs = CaseMarkPdfGenerator.GenerateAll(mixed, args[1], new DateTime(2026, 9, 22));
Check(pdfs.Count == 1 && File.Exists(pdfs[0]), "one case mark PDF is generated for one carton");

var requestId = Guid.NewGuid();
var service = new FileGenerationService(
    Options.Create(new FileGeneratorOptions { OutputDirectory = args[1] }),
    new TestEnvironment(args[1]),
    new EmptyPickupLookup(),
    new EmptyCountryLookup(),
    NullLogger<FileGenerationService>.Instance);
var failed = await service.GenerateAsync(new FileGenerationJob
{
    RequestId = requestId,
    InputFilePath = inputPath
});
Check(!failed.Success && failed.ErrorMessage == "Please check County of origin",
    "an unmapped origin fails the entire generation request");
Check(!Directory.Exists(Path.Combine(args[1], requestId.ToString("D"))),
    "origin validation fails before any output folder or file is created");

foreach (var badCode in new[] { "", "ZZZ" })
{
    try
    {
        FileGenerationService.ResolveCountryNames(
            [new ShippingAdviceRow { CountryOfOriginCode = badCode }], countries);
        throw new Exception($"Expected rejection of '{badCode}'.");
    }
    catch (InvalidOperationException ex) when (ex.Message == "Please check County of origin")
    {
        Console.WriteLine($"PASS: rejects unknown/empty origin '{badCode}'");
    }
}

PickupNoticeChecks.Run(args[1]);
if (args.Length == 3)
{
    PickupNoticeChecks.RunUploadedWorkbook(args[2], args[1]);
}

Console.WriteLine("File generation checks passed.");

static void Check(bool success, string description)
{
    if (!success) throw new Exception("FAIL: " + description);
    Console.WriteLine("PASS: " + description);
}

sealed class EmptyPickupLookup : IPickUpLocationLookup
{
    public Task<IReadOnlyDictionary<string, PickUpLocationInfo>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, PickUpLocationInfo>>(new Dictionary<string, PickUpLocationInfo>());
}

sealed class EmptyCountryLookup : ICountryOfOriginLookup
{
    public Task<IReadOnlyDictionary<string, string>> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
}

sealed class TestEnvironment(string root) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Development";
    public string ApplicationName { get; set; } = "FileGenerationChecks";
    public string ContentRootPath { get; set; } = root;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
