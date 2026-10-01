using ICPFileGenerator.Models;
using Microsoft.Extensions.Options;

namespace ICPFileGenerator.Services;

public sealed class FileGenerationService : IFileGenerationService
{
    private readonly FileGeneratorOptions _options;
    private readonly IHostEnvironment _env;
    private readonly IPickUpLocationLookup _pickUpLocationLookup;
    private readonly ICountryOfOriginLookup _countryOfOriginLookup;
    private readonly ILogger<FileGenerationService> _logger;

    public FileGenerationService(
        IOptions<FileGeneratorOptions> options,
        IHostEnvironment env,
        IPickUpLocationLookup pickUpLocationLookup,
        ICountryOfOriginLookup countryOfOriginLookup,
        ILogger<FileGenerationService> logger)
    {
        _options = options.Value;
        _env = env;
        _pickUpLocationLookup = pickUpLocationLookup;
        _countryOfOriginLookup = countryOfOriginLookup;
        _logger = logger;
    }

    public async Task<FileGenerationResult> GenerateAsync(
        FileGenerationJob job,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job.InputFilePath) || !File.Exists(job.InputFilePath))
            {
                return FileGenerationResult.Fail(
                    $"Input file not found: {job.InputFilePath}");
            }

            var rows = ShippingAdviceSheetReader.Read(job.InputFilePath);
            if (rows.Count == 0)
            {
                return FileGenerationResult.Fail(
                    $"No data rows found in the first worksheet (from row {ShippingAdviceSheetReader.DataStartRow}).");
            }

            var countries = await _countryOfOriginLookup.LoadAsync(cancellationToken);
            ResolveCountryNames(rows, countries);

            var stampDate = DateTime.Now;
            var outputRoot = ResolveOutputRoot();
            var requestFolder = Path.Combine(outputRoot, job.RequestId.ToString("D"));
            Directory.CreateDirectory(requestFolder);

            var pickUpBySloc = await _pickUpLocationLookup.LoadAsync(cancellationToken);
            var excelPath = PickupNoticeExcelGenerator.Generate(rows, requestFolder, stampDate, pickUpBySloc);
            var pdfPaths = CaseMarkPdfGenerator.GenerateAll(rows, requestFolder, stampDate);

            _logger.LogInformation(
                "Generated export folder={Folder} excel={Excel} pdfCount={PdfCount} RequestId={RequestId}",
                requestFolder,
                Path.GetFileName(excelPath),
                pdfPaths.Count,
                job.RequestId);

            return FileGenerationResult.Ok(requestFolder);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File generation failed RequestId={RequestId}", job.RequestId);
            return FileGenerationResult.Fail(ex.Message);
        }
    }

    public static void ResolveCountryNames(
        IReadOnlyList<ShippingAdviceRow> rows,
        IReadOnlyDictionary<string, string> countries)
    {
        foreach (var row in rows)
        {
            var code = row.CountryOfOriginCode.Trim();
            if (string.IsNullOrEmpty(code)
                || !countries.TryGetValue(code, out var country)
                || string.IsNullOrWhiteSpace(country))
            {
                throw new InvalidOperationException("Please check County of origin");
            }

            // The Country of Origin setting uses the ISO label; the case mark uses the common name.
            row.CountryOfOriginName = code.Equals("TW", StringComparison.OrdinalIgnoreCase)
                && country.Equals("Taiwan, Province of China", StringComparison.OrdinalIgnoreCase)
                ? "Taiwan"
                : country;
        }
    }

    private string ResolveOutputRoot()
    {
        var configured = _options.OutputDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(configured))
        {
            configured = "Output";
        }

        return Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
    }
}
