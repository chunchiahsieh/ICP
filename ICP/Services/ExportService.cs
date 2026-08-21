using System.IO.Compression;
using System.Net.Http.Json;
using ICP.Data;
using ICP.Models.Icp;
using ICP.Models.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public interface IExportService
{
    Task<IReadOnlyList<ExportRequest>> ListAsync(CancellationToken cancellationToken = default);

    Task<ExportRequest> UploadAndNotifyHubAsync(IFormFile file, CancellationToken cancellationToken = default);

    Task<ExportRequest?> GetAsync(Guid requestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExportOutputFileInfo>> ListOutputFilesAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string ContentType, string DownloadName)?> OpenOutputFileAsync(
        Guid requestId,
        string fileName,
        CancellationToken cancellationToken = default);

    Task<(Stream Stream, string DownloadName)?> OpenOutputZipAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    /// <summary>Relative/shared folder path for UI (not local drive path).</summary>
    string GetDisplayFolderPath(Guid requestId);
}

public sealed class ExportOutputFileInfo
{
    public string FileName { get; init; } = string.Empty;

    public string Extension { get; init; } = string.Empty;

    public long SizeBytes { get; init; }
}

public sealed class ExportService : IExportService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HubClientOptions _hub;
    private readonly ExportClientOptions _export;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ExportService> _logger;

    public ExportService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOptions> integrationOptions,
        IWebHostEnvironment env,
        ILogger<ExportService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _hub = integrationOptions.Value.Hub;
        _export = integrationOptions.Value.Export;
        _env = env;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExportRequest>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.ExportRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreateTime)
            .Take(50)
            .ToListAsync(cancellationToken);

    public Task<ExportRequest?> GetAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        _db.ExportRequests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

    public async Task<ExportRequest> UploadAndNotifyHubAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException("File is required.");
        }

        var requestId = Guid.NewGuid();
        var uploadRoot = Path.Combine(_env.ContentRootPath, "uploads", "export");
        Directory.CreateDirectory(uploadRoot);
        var safeName = Path.GetFileName(file.FileName);
        var storedName = $"{requestId:N}_{safeName}";
        var storedPath = Path.Combine(uploadRoot, storedName);

        await using (var stream = File.Create(storedPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var entity = new ExportRequest
        {
            Id = requestId,
            FileName = safeName,
            StoredPath = storedPath,
            Status = ExportRequestStatuses.Pending,
            CreateTime = DateTime.UtcNow
        };
        _db.ExportRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            var client = _httpClientFactory.CreateClient("IntegrationHub");
            var payload = new
            {
                requestId = entity.Id,
                fileName = entity.FileName,
                sourceRecordId = entity.Id.ToString("D"),
                storedPath = entity.StoredPath
            };
            using var response = await client.PostAsJsonAsync(
                "api/export/export-requests",
                payload,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Notified Hub for ExportRequest {RequestId}", entity.Id);
        }
        catch (Exception ex)
        {
            entity.Status = ExportRequestStatuses.Failed;
            entity.ErrorMessage = $"Hub notify failed: {ex.Message}";
            entity.UpdateTime = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }

        return entity;
    }

    public async Task<IReadOnlyList<ExportOutputFileInfo>> ListOutputFilesAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var folder = await ResolveOutputFolderAsync(requestId, cancellationToken);
        if (folder is null || !Directory.Exists(folder))
        {
            return Array.Empty<ExportOutputFileInfo>();
        }

        return Directory.EnumerateFiles(folder)
            .Select(path => new FileInfo(path))
            .Where(f => IsAllowedExtension(f.Extension))
            .OrderBy(f => IsPickupNoticeExcel(f.Name) ? 0 : 1)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new ExportOutputFileInfo
            {
                FileName = f.Name,
                Extension = f.Extension,
                SizeBytes = f.Length
            })
            .ToList();
    }

    private static bool IsPickupNoticeExcel(string fileName) =>
        fileName.StartsWith("PickupNotice_", StringComparison.OrdinalIgnoreCase)
        && fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase);

    public async Task<(Stream Stream, string ContentType, string DownloadName)?> OpenOutputFileAsync(
        Guid requestId,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var fullPath = await ResolveSafeFilePathAsync(requestId, fileName, cancellationToken);
        if (fullPath is null)
        {
            return null;
        }

        var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        var contentType = GetContentType(Path.GetExtension(fullPath));
        return (stream, contentType, Path.GetFileName(fullPath));
    }

    public async Task<(Stream Stream, string DownloadName)?> OpenOutputZipAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        var folder = await ResolveOutputFolderAsync(requestId, cancellationToken);
        if (folder is null || !Directory.Exists(folder))
        {
            return null;
        }

        var files = Directory.EnumerateFiles(folder)
            .Where(p => IsAllowedExtension(Path.GetExtension(p)))
            .ToList();
        if (files.Count == 0)
        {
            return null;
        }

        var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var path in files)
            {
                archive.CreateEntryFromFile(path, Path.GetFileName(path), CompressionLevel.Fastest);
            }
        }

        memory.Position = 0;
        return (memory, $"{requestId:D}.zip");
    }

    public string GetDisplayFolderPath(Guid requestId)
    {
        var root = _export.OutputDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(root))
        {
            root = "ICPFileGenerator";
        }

        // Prefer configured relative name; if absolute, use last folder segment only.
        var folderName = Path.IsPathRooted(root)
            ? new DirectoryInfo(root).Name
            : root.Replace('\\', '/').Trim('/');

        return $"{folderName}/{requestId:D}";
    }

    private async Task<string?> ResolveOutputFolderAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var entity = await GetAsync(requestId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(entity.OutputFilePath))
        {
            return entity.OutputFilePath;
        }

        var root = _export.OutputDirectory?.Trim();
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        var absoluteRoot = Path.IsPathRooted(root)
            ? root
            : Path.GetFullPath(Path.Combine(_env.ContentRootPath, root));
        return Path.Combine(absoluteRoot, requestId.ToString("D"));
    }

    private async Task<string?> ResolveSafeFilePathAsync(
        Guid requestId,
        string fileName,
        CancellationToken cancellationToken)
    {
        var folder = await ResolveOutputFolderAsync(requestId, cancellationToken);
        if (folder is null || !Directory.Exists(folder))
        {
            return null;
        }

        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || !IsAllowedExtension(Path.GetExtension(safeName)))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(folder, safeName));
        var folderFull = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(folderFull, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return File.Exists(fullPath) ? fullPath : null;
    }

    private static bool IsAllowedExtension(string extension) =>
        extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    private static string GetContentType(string extension) =>
        extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
