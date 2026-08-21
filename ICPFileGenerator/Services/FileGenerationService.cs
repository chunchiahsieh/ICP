using ICPFileGenerator.Models;
using Microsoft.Extensions.Options;

namespace ICPFileGenerator.Services;

public sealed class FileGenerationService : IFileGenerationService
{
    private readonly FileGeneratorOptions _options;
    private readonly IHostEnvironment _env;
    private readonly ILogger<FileGenerationService> _logger;

    public FileGenerationService(
        IOptions<FileGeneratorOptions> options,
        IHostEnvironment env,
        ILogger<FileGenerationService> logger)
    {
        _options = options.Value;
        _env = env;
        _logger = logger;
    }

    public Task<FileGenerationResult> GenerateAsync(
        FileGenerationJob job,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(job.InputFilePath) || !File.Exists(job.InputFilePath))
            {
                return Task.FromResult(FileGenerationResult.Fail(
                    $"Input file not found: {job.InputFilePath}"));
            }

            var stampDate = DateTime.Now;
            var outputRoot = ResolveOutputRoot();
            var requestFolder = Path.Combine(outputRoot, job.RequestId.ToString("D"));
            Directory.CreateDirectory(requestFolder);

            var rows = ShippingAdviceSheetReader.Read(job.InputFilePath);
            if (rows.Count == 0)
            {
                return Task.FromResult(FileGenerationResult.Fail(
                    $"No data rows found in sheet '{ShippingAdviceSheetReader.SourceSheetName}' (from row {ShippingAdviceSheetReader.DataStartRow})."));
            }

            var excelPath = PickupNoticeExcelGenerator.Generate(rows, requestFolder, stampDate);
            var pdfPaths = CaseMarkPdfGenerator.GenerateAll(rows, requestFolder, stampDate);

            _logger.LogInformation(
                "Generated export folder={Folder} excel={Excel} pdfCount={PdfCount} RequestId={RequestId}",
                requestFolder,
                Path.GetFileName(excelPath),
                pdfPaths.Count,
                job.RequestId);

            return Task.FromResult(FileGenerationResult.Ok(requestFolder));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File generation failed RequestId={RequestId}", job.RequestId);
            return Task.FromResult(FileGenerationResult.Fail(ex.Message));
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
