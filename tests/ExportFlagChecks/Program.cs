using ClosedXML.Excel;
using ICP.Models.Integration;
using ICP.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
    Console.WriteLine("PASS " + name);
}

byte[] Workbook(params (string Invoice, string Flag)[] rows)
{
    using var workbook = new XLWorkbook();
    var sheet = workbook.Worksheets.Add("Shipping advice");
    sheet.Cell(1, 1).Value = "Invoice No.";
    sheet.Cell(1, 2).Value = "No Charge Flag";
    for (var i = 0; i < rows.Length; i++)
    {
        sheet.Cell(i + 2, 1).Value = rows[i].Invoice;
        sheet.Cell(i + 2, 2).Value = rows[i].Flag;
    }
    using var stream = new MemoryStream();
    workbook.SaveAs(stream);
    return stream.ToArray();
}

void Valid(string name, params (string Invoice, string Flag)[] rows)
{
    using var stream = new MemoryStream(Workbook(rows));
    ShippingAdviceNoChargeFlagValidator.Validate(stream);
    Check(true, name);
}

string Reject(params (string Invoice, string Flag)[] rows)
{
    using var stream = new MemoryStream(Workbook(rows));
    try { ShippingAdviceNoChargeFlagValidator.Validate(stream); }
    catch (InvalidOperationException ex) { return ex.Message; }
    throw new Exception("Expected inconsistent flag rejection");
}

Valid("same invoice X on every row", ("INV1", "X"), (" inv1 ", "x "));
Valid("same invoice all non-X", ("INV1", ""), ("INV1", "N"));
Valid("different invoices may use different flags", ("INV1", "X"), ("INV2", ""));
var mismatch = Reject(("INV1", "X"), ("INV1", ""));
Check(mismatch.Contains("INV1") && mismatch.Contains("第 2 列") && mismatch.Contains("第 3 列"),
    "mixed invoice flags identify invoice and source rows");
Check(Reject(("INV1", ""), ("INV1", "X")).Contains("不一致"),
    "non-X followed by X is rejected");

var root = Path.Combine(Path.GetTempPath(), "icp-export-flag-check-" + Guid.NewGuid().ToString("N"));
var factory = new CountingHttpClientFactory();
var options = Options.Create(new IntegrationOptions
{
    Export = new ExportClientOptions { InputDirectory = Path.Combine(root, "input") }
});
var service = new ExportService(null!, factory, options,
    new TestEnvironment { ContentRootPath = root }, NullLogger<ExportService>.Instance);
var invalidBytes = Workbook(("INV1", "X"), ("INV1", ""));
var file = new FormFile(new MemoryStream(invalidBytes), 0, invalidBytes.Length, "file", "shipping.xlsx");
try { await service.UploadAndNotifyHubAsync(file); throw new Exception("Expected upload rejection"); }
catch (InvalidOperationException ex) when (ex.Message.Contains("INV1")) { }
Check(!Directory.Exists(root) && factory.Calls == 0,
    "invalid upload creates no file or request and never contacts Hub");

Console.WriteLine($"{checks} checks passed; no database or Hub access.");

sealed class CountingHttpClientFactory : IHttpClientFactory
{
    public int Calls { get; private set; }
    public HttpClient CreateClient(string name) { Calls++; throw new Exception("Hub should not be called"); }
}

sealed class TestEnvironment : IWebHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "ExportFlagChecks";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
