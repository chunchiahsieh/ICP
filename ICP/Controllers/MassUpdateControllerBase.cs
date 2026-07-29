using ICP.Helpers;
using ICP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public abstract class MassUpdateControllerBase : Controller
{
    private const int MaxSizeMb = 50;
    private const string SampleFileName = "Mass Update Sample File.xlsx";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx", ".xls", ".csv"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly MassUpdateImportService _importService;
    private readonly MassUpdatePendingFileStore _pendingFileStore;
    private readonly ILogger _logger;

    protected MassUpdateControllerBase(
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> localizer,
        MassUpdateImportService importService,
        MassUpdatePendingFileStore pendingFileStore,
        ILogger logger)
    {
        _environment = environment;
        _localizer = localizer;
        _importService = importService;
        _pendingFileStore = pendingFileStore;
        _logger = logger;
    }

    protected abstract string ViewPath { get; }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["MaxSizeMb"] = MaxSizeMb;
        return View(ViewPath);
    }

    [HttpGet]
    public IActionResult DownloadSample()
    {
        var path = Path.Combine(_environment.ContentRootPath, "Files", SampleFileName);
        if (!System.IO.File.Exists(path))
        {
            return NotFound();
        }

        return PhysicalFile(
            path,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            SampleFileName);
    }

    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return Json(new { success = false, message = _localizer["Function.AddDiSa.NoFileSelected"].Value });
        }

        if (file.Length > MaxSizeMb * 1024L * 1024L)
        {
            return Json(new
            {
                success = false,
                message = string.Format(_localizer["Function.AddDiSa.MaxSizeExceeded"].Value, MaxSizeMb)
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return Json(new { success = false, message = _localizer["Function.AddDiSa.InvalidFileType"].Value });
        }

        var safeFileName = Path.GetFileName(file.FileName);
        var directory = MassUpdateImportService.ResolveStorageDirectory(_environment);
        Directory.CreateDirectory(directory);
        var storedPath = Path.GetFullPath(
            Path.Combine(directory, $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeFileName}"));

        try
        {
            await using (var stream = System.IO.File.Create(storedPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var rows = await _importService.ParseAsync(storedPath, cancellationToken);
            var preview = await _importService.BuildPreviewRowsAsync(rows, cancellationToken);
            _pendingFileStore.Add(HttpContext, storedPath);
            var duplicateCount = preview.Count(row => row.IsDuplicateInFile);
            var matchedCount = preview.Count(row => !row.IsNotFound);
            var notFoundCount = preview.Count - matchedCount;
            var canSave = preview.Count > 0 && preview.All(row => row.CanSave);

            return Json(new
            {
                success = true,
                message = string.Format(
                    _localizer["Function.MassUpdate.ParseSuccess"].Value,
                    preview.Count,
                    matchedCount,
                    notFoundCount),
                canSave,
                previewCount = preview.Count,
                matchedCount,
                notFoundCount,
                duplicateCount,
                filePath = storedPath,
                fileName = safeFileName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MassUpdate upload parse failed: {FileName}", safeFileName);
            if (System.IO.File.Exists(storedPath))
            {
                System.IO.File.Delete(storedPath);
            }

            _pendingFileStore.Remove(HttpContext, storedPath);
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Query(string? filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedPath = RequirePendingFile(filePath);
            var rows = await _importService.ParseAsync(normalizedPath, cancellationToken);
            var preview = await _importService.BuildPreviewRowsAsync(rows, cancellationToken);
            return PartialView("~/Views/FUNCTION/Shared/MassUpdate.List.cshtml", preview);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MassUpdate preview failed");
            return Content($"<div class=\"alert alert-danger m-3\">{System.Net.WebUtility.HtmlEncode(ex.Message)}</div>");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Save(string? filePath, CancellationToken cancellationToken = default)
    {
        string normalizedPath;
        try
        {
            normalizedPath = RequirePendingFile(filePath);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }

        try
        {
            var result = await _importService.SaveAsync(
                normalizedPath,
                CrudAuditHelper.ResolveUserName(User.Identity?.Name),
                cancellationToken);
            DeletePendingFile(normalizedPath);
            return Json(new
            {
                success = true,
                message = string.Format(
                    _localizer["Function.MassUpdate.SaveSuccess"].Value,
                    result.UpdatedHeaderCount,
                    result.MatchedExcelRowCount,
                    result.NotFoundExcelRowCount),
                result.UpdatedHeaderCount,
                result.MatchedExcelRowCount,
                result.NotFoundExcelRowCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MassUpdate save failed: {FilePath}", normalizedPath);
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public IActionResult CancelPending(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            try
            {
                DeletePendingFile(MassUpdateImportService.ValidateAndNormalizeStoredFilePath(filePath, _environment));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MassUpdate cancel pending failed");
            }
        }

        return Json(new { success = true });
    }

    private string RequirePendingFile(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException(_localizer["Function.MassUpdate.PleaseUploadFirst"].Value);
        }

        var normalized = MassUpdateImportService.ValidateAndNormalizeStoredFilePath(filePath, _environment);
        if (!_pendingFileStore.Contains(HttpContext, normalized))
        {
            throw new InvalidOperationException(_localizer["Function.MassUpdate.PleaseUploadFirst"].Value);
        }

        return normalized;
    }

    private void DeletePendingFile(string normalizedPath)
    {
        _pendingFileStore.Remove(HttpContext, normalizedPath);
        if (System.IO.File.Exists(normalizedPath))
        {
            System.IO.File.Delete(normalizedPath);
        }
    }
}
