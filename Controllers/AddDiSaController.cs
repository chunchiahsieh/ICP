using ICP;
using ICP.Helpers;
using ICP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class AddDiSaController : Controller
{
    private const int MaxSizeMb = 50;
    private const string TemplateFileName = "AddDiSaTemplate.xlsx";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".xlsx",
        ".xls"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly AddDiSaImportService _importService;
    private readonly AddDiSaPendingFileStore _pendingFileStore;
    private readonly ILogger<AddDiSaController> _logger;

    public AddDiSaController(
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> localizer,
        AddDiSaImportService importService,
        AddDiSaPendingFileStore pendingFileStore,
        ILogger<AddDiSaController> logger)
    {
        _environment = environment;
        _localizer = localizer;
        _importService = importService;
        _pendingFileStore = pendingFileStore;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["MaxSizeMb"] = MaxSizeMb;
        return View("~/Views/FUNCTION/AddDiSa/View.cshtml");
    }

    [HttpGet]
    public IActionResult DownloadTemplate()
    {
        var templatePath = Path.Combine(_environment.ContentRootPath, "Files", TemplateFileName);
        if (!System.IO.File.Exists(templatePath))
        {
            return NotFound();
        }

        return PhysicalFile(
            templatePath,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TemplateFileName);
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
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return Json(new { success = false, message = _localizer["Function.AddDiSa.InvalidFileType"].Value });
        }

        var uploadDirectory = AddDiSaImportService.ResolveStorageDirectory(_environment);
        Directory.CreateDirectory(uploadDirectory);

        var storedFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeFileName}";
        var storedPath = Path.GetFullPath(Path.Combine(uploadDirectory, storedFileName));

        try
        {
            await using (var stream = System.IO.File.Create(storedPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var rows = await _importService.ParseAsync(storedPath, cancellationToken);
            var previewRows = await _importService.BuildPreviewRowsAsync(rows, cancellationToken);
            _pendingFileStore.Add(HttpContext, storedPath);

            var headerCount = rows
                .GroupBy(r => $"{r.InvoiceNo}\u001f{r.TetPo}", StringComparer.OrdinalIgnoreCase)
                .Count();

            var canSave = previewRows.Count > 0 && previewRows.All(r => r.CanUpload);
            var message = canSave
                ? string.Format(
                    _localizer["Function.AddDiSa.ParseSuccess"].Value,
                    headerCount,
                    rows.Count)
                : _localizer["Function.AddDiSa.CannotSaveMessage"].Value;

            return Json(new
            {
                success = true,
                message,
                canSave,
                previewCount = rows.Count,
                headerCount,
                detailCount = rows.Count,
                filePath = storedPath,
                fileName = safeFileName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddDiSa upload parse failed: {FileName}", safeFileName);

            if (System.IO.File.Exists(storedPath))
            {
                System.IO.File.Delete(storedPath);
            }

            _pendingFileStore.Remove(HttpContext, storedPath);

            return Json(new
            {
                success = false,
                message = string.IsNullOrWhiteSpace(ex.Message)
                    ? _localizer["Function.AddDiSa.UploadFailed"].Value
                    : ex.Message
            });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Query(
        string? filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return BadRequest();
        }

        try
        {
            var normalizedPath = AddDiSaImportService.ValidateAndNormalizeStoredFilePath(filePath, _environment);
            if (!_pendingFileStore.Contains(HttpContext, normalizedPath))
            {
                var msg = System.Net.WebUtility.HtmlEncode(_localizer["Function.AddDiSa.PleaseUploadFirst"].Value);
                return Content($"<div class=\"alert alert-warning m-3\">{msg}</div>");
            }

            var rows = await _importService.ParseAsync(normalizedPath, cancellationToken);
            var previewRows = await _importService.BuildPreviewRowsAsync(rows, cancellationToken);
            return PartialView("~/Views/FUNCTION/AddDiSa/View.List.cshtml", previewRows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddDiSa preview query failed");
            return Content($"<div class=\"alert alert-danger m-3\">{System.Net.WebUtility.HtmlEncode(ex.Message)}</div>");
        }
    }

    [HttpPost]
    public async Task<IActionResult> Save(
        string? filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Json(new { success = false, message = _localizer["Function.AddDiSa.PleaseUploadFirst"].Value });
        }

        string normalizedPath;
        try
        {
            normalizedPath = AddDiSaImportService.ValidateAndNormalizeStoredFilePath(filePath, _environment);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }

        if (!_pendingFileStore.Contains(HttpContext, normalizedPath))
        {
            return Json(new { success = false, message = _localizer["Function.AddDiSa.PleaseUploadFirst"].Value });
        }

        try
        {
            var result = await _importService.SaveAsync(
                normalizedPath,
                CrudAuditHelper.ResolveUserName(User.Identity?.Name),
                cancellationToken);

            _pendingFileStore.Remove(HttpContext, normalizedPath);

            var message = string.Format(
                _localizer["Function.AddDiSa.SaveSuccess"].Value,
                result.HeaderCount,
                result.DetailCount);

            return Json(new
            {
                success = true,
                message,
                headerCount = result.HeaderCount,
                detailCount = result.DetailCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AddDiSa save failed: {FilePath}", normalizedPath);
            return Json(new
            {
                success = false,
                message = string.IsNullOrWhiteSpace(ex.Message)
                    ? _localizer["Function.AddDiSa.SaveFailed"].Value
                    : ex.Message
            });
        }
    }

    [HttpPost]
    public IActionResult CancelPending(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Json(new { success = true });
        }

        try
        {
            var normalizedPath = AddDiSaImportService.ValidateAndNormalizeStoredFilePath(filePath, _environment);
            _pendingFileStore.Remove(HttpContext, normalizedPath);
            if (System.IO.File.Exists(normalizedPath))
            {
                System.IO.File.Delete(normalizedPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AddDiSa cancel pending failed");
        }

        return Json(new { success = true });
    }
}
