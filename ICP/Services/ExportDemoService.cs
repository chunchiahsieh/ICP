using System.Net.Http.Json;
using ICP.Data;
using ICP.Models.Icp;
using ICP.Models.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ICP.Services;

public interface IExportDemoService
{
    Task<IReadOnlyList<ExportRequest>> ListAsync(CancellationToken cancellationToken = default);

    Task<ExportRequest> UploadAndNotifyHubAsync(IFormFile file, CancellationToken cancellationToken = default);
}

public sealed class ExportDemoService : IExportDemoService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HubClientOptions _hub;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ExportDemoService> _logger;

    public ExportDemoService(
        ApplicationDbContext db,
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOptions> integrationOptions,
        IWebHostEnvironment env,
        ILogger<ExportDemoService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _hub = integrationOptions.Value.Hub;
        _env = env;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExportRequest>> ListAsync(CancellationToken cancellationToken = default) =>
        await _db.ExportRequests
            .AsNoTracking()
            .OrderByDescending(x => x.CreateTime)
            .Take(50)
            .ToListAsync(cancellationToken);

    public async Task<ExportRequest> UploadAndNotifyHubAsync(
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException("File is required.");
        }

        var requestId = Guid.NewGuid();
        var uploadRoot = Path.Combine(_env.ContentRootPath, "uploads", "export-demo");
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
            var client = _httpClientFactory.CreateClient("HubDemo");
            var payload = new
            {
                requestId = entity.Id,
                fileName = entity.FileName,
                sourceRecordId = entity.Id.ToString("D"),
                storedPath = entity.StoredPath
            };
            using var response = await client.PostAsJsonAsync(
                "api/demo/export-requests",
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
}
