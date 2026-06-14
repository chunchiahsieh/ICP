using System.Text.Json;

using ICP.Data;

using ICP.Helpers;

using ICP.Models;

using ICP.Models.Icp;

using ICP.Services;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

using Microsoft.Extensions.Options;



namespace ICP.Controllers;



public class ForwarderDataUploadController : Controller

{

    public const string PendingFilePathsSessionKey = "ForwarderPendingFilePaths";

    private const string TemplateFileName = "ForwarderDataUploadTemplate.xlsx";

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)

    {

        ".xlsx",

        ".xls",

        ".csv"

    };



    private readonly ApplicationDbContext _db;

    private readonly IWebHostEnvironment _environment;

    private readonly ForwarderDataUploadOptions _options;

    private readonly ForwarderDataImportService _importService;

    private readonly IStringLocalizer<SharedResource> _localizer;

    private readonly ILogger<ForwarderDataUploadController> _logger;



    public ForwarderDataUploadController(

        ApplicationDbContext db,

        IWebHostEnvironment environment,

        IOptions<ForwarderDataUploadOptions> options,

        ForwarderDataImportService importService,

        IStringLocalizer<SharedResource> localizer,

        ILogger<ForwarderDataUploadController> logger)

    {

        _db = db;

        _environment = environment;

        _options = options.Value;

        _importService = importService;

        _localizer = localizer;

        _logger = logger;

    }



    [HttpGet]

    public IActionResult Index()

    {

        ViewData["MaxSizeMb"] = _options.MaxSizeMb;

        return View("~/Views/FORWARDER/ForwarderDataUpload/View.cshtml");

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

    public async Task<IActionResult> Query(

        [FromForm] string? filePath,

        [FromForm] bool preview = true,

        CancellationToken cancellationToken = default)

    {

        if (string.IsNullOrWhiteSpace(filePath))

        {

            return PartialView("~/Views/FORWARDER/ForwarderDataUpload/View.List.cshtml", new ForwarderDataUploadListViewModel());

        }



        var normalizedPath = ForwarderDataImportService.ValidateAndNormalizeStoredFilePath(

            filePath,

            _environment,

            _options);



        if (preview)

        {

            var createUser = CrudAuditHelper.ResolveUserName(User.Identity?.Name);

            var rows = await _importService.ParseAsync(normalizedPath, createUser, cancellationToken);

            return PartialView("~/Views/FORWARDER/ForwarderDataUpload/View.List.cshtml", new ForwarderDataUploadListViewModel

            {

                ListData = rows

            });

        }



        var list = await _db.ForwarderDataUploads

            .AsNoTracking()

            .Where(x => x.FilePath == normalizedPath)

            .OrderBy(x => x.Id)

            .ToListAsync(cancellationToken);



        return PartialView("~/Views/FORWARDER/ForwarderDataUpload/View.List.cshtml", new ForwarderDataUploadListViewModel

        {

            ListData = list

        });

    }



    [HttpPost]

    [RequestSizeLimit(52_428_800)]

    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken = default)

    {

        if (file is null || file.Length == 0)

        {

            return Json(new { success = false, message = "未選擇檔案" });

        }



        if (file.Length > _options.MaxSizeBytes)

        {

            return Json(new { success = false, message = $"檔案超過 {_options.MaxSizeMb}MB" });

        }



        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))

        {

            return Json(new { success = false, message = "僅支援 .xlsx、.xls、.csv 格式" });

        }



        var safeFileName = Path.GetFileName(file.FileName);

        if (string.IsNullOrWhiteSpace(safeFileName))

        {

            return Json(new { success = false, message = "檔案名稱無效" });

        }



        var uploadDirectory = ForwarderDataImportService.ResolveStorageDirectory(_environment, _options);

        Directory.CreateDirectory(uploadDirectory);



        var storedFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeFileName}";

        var storedPath = Path.GetFullPath(Path.Combine(uploadDirectory, storedFileName));



        try

        {

            await using (var stream = System.IO.File.Create(storedPath))

            {

                await file.CopyToAsync(stream, cancellationToken);

            }



            var createUser = CrudAuditHelper.ResolveUserName(User.Identity?.Name);

            var rows = await _importService.ParseAsync(storedPath, createUser, cancellationToken);

            AddPendingFilePath(HttpContext.Session, storedPath);



            _logger.LogInformation(

                "Forwarder data parsed for preview: {FileName} -> {StoredPath}, Count={Count}",

                safeFileName,

                storedPath,

                rows.Count);



            var message = string.Format(

                _localizer["Forwarder.ForwarderDataUpload.PreviewMessage"].Value,

                rows.Count);



            return Json(new

            {

                success = true,

                message,

                previewCount = rows.Count,

                filePath = storedPath,

                fileName = safeFileName

            });

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Forwarder data preview parse failed: {FileName}", safeFileName);



            if (System.IO.File.Exists(storedPath))

            {

                System.IO.File.Delete(storedPath);

            }



            RemovePendingFilePath(HttpContext.Session, storedPath);

            return Json(new { success = false, message = ex.Message });

        }

    }



    [HttpPost]

    public async Task<IActionResult> Save([FromForm] string? filePath, CancellationToken cancellationToken = default)

    {

        if (string.IsNullOrWhiteSpace(filePath))

        {

            return Json(new { success = false, message = "未指定檔案" });

        }



        try

        {

            var normalizedPath = ForwarderDataImportService.ValidateAndNormalizeStoredFilePath(

                filePath,

                _environment,

                _options);



            if (!IsPendingFilePath(HttpContext.Session, normalizedPath))

            {

                return Json(new { success = false, message = "請先上傳檔案後再儲存" });

            }



            if (await _db.ForwarderDataUploads.AnyAsync(x => x.FilePath == normalizedPath, cancellationToken))

            {

                return Json(new

                {

                    success = false,

                    message = _localizer["Forwarder.ForwarderDataUpload.AlreadySaved"].Value

                });

            }



            var createUser = CrudAuditHelper.ResolveUserName(User.Identity?.Name);

            var result = await _importService.SaveAsync(normalizedPath, createUser, cancellationToken);



            RemovePendingFilePath(HttpContext.Session, normalizedPath);



            _logger.LogInformation(

                "Forwarder data saved: {StoredPath}, Count={Count}",

                normalizedPath,

                result.ImportedCount);



            var message = string.Format(

                _localizer["Forwarder.ForwarderDataUpload.SaveSuccessMessage"].Value,

                result.ImportedCount);



            return Json(new

            {

                success = result.Success,

                message,

                importedCount = result.ImportedCount,

                filePath = result.FilePath

            });

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Forwarder data save failed: {FilePath}", filePath);

            return Json(new { success = false, message = ex.Message });

        }

    }



    [HttpPost]

    public IActionResult CancelPending([FromForm] string? filePath)

    {

        if (string.IsNullOrWhiteSpace(filePath))

        {

            return Json(new { success = false, message = "未指定檔案" });

        }



        try

        {

            var normalizedPath = ValidateStoredFilePathLocation(filePath);



            if (IsPendingFilePath(HttpContext.Session, normalizedPath))

            {

                RemovePendingFilePath(HttpContext.Session, normalizedPath);

            }

            else

            {

                _logger.LogWarning(

                    "Forwarder pending file cancel: not in session {StoredPath}",

                    normalizedPath);

            }



            if (System.IO.File.Exists(normalizedPath))

            {

                System.IO.File.Delete(normalizedPath);

            }



            _logger.LogInformation("Forwarder pending file cancelled: {StoredPath}", normalizedPath);



            return Json(new { success = true });

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Forwarder pending file cancel failed: {FilePath}", filePath);

            return Json(new { success = false, message = ex.Message });

        }

    }



    private string ValidateStoredFilePathLocation(string storedFilePath)

    {

        var uploadDirectory = Path.GetFullPath(

            ForwarderDataImportService.ResolveStorageDirectory(_environment, _options));

        var normalizedPath = Path.GetFullPath(storedFilePath.Trim());



        if (!normalizedPath.StartsWith(uploadDirectory, StringComparison.OrdinalIgnoreCase))

        {

            throw new InvalidOperationException("檔案路徑無效");

        }



        return normalizedPath;

    }



    private static List<string> GetPendingFilePaths(ISession session)

    {

        var json = session.GetString(PendingFilePathsSessionKey);

        if (string.IsNullOrWhiteSpace(json))

        {

            return [];

        }



        try

        {

            return JsonSerializer.Deserialize<List<string>>(json) ?? [];

        }

        catch (JsonException)

        {

            return [];

        }

    }



    private static void SetPendingFilePaths(ISession session, List<string> paths)

    {

        if (paths.Count == 0)

        {

            session.Remove(PendingFilePathsSessionKey);

            return;

        }



        session.SetString(PendingFilePathsSessionKey, JsonSerializer.Serialize(paths));

    }



    private static void AddPendingFilePath(ISession session, string path)

    {

        var normalized = Path.GetFullPath(path.Trim());

        var paths = GetPendingFilePaths(session);

        if (paths.All(p => !string.Equals(Path.GetFullPath(p.Trim()), normalized, StringComparison.OrdinalIgnoreCase)))

        {

            paths.Add(normalized);

        }



        SetPendingFilePaths(session, paths);

    }



    private static void RemovePendingFilePath(ISession session, string path)

    {

        var normalized = Path.GetFullPath(path.Trim());

        var paths = GetPendingFilePaths(session);

        paths.RemoveAll(p => string.Equals(Path.GetFullPath(p.Trim()), normalized, StringComparison.OrdinalIgnoreCase));

        SetPendingFilePaths(session, paths);

    }



    private static bool IsPendingFilePath(ISession session, string path)

    {

        var normalized = Path.GetFullPath(path.Trim());

        return GetPendingFilePaths(session)

            .Any(p => string.Equals(Path.GetFullPath(p.Trim()), normalized, StringComparison.OrdinalIgnoreCase));

    }

}


