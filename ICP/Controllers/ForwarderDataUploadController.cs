using ICP.Data;

using ICP.Helpers;

using ICP.Models;

using ICP.Models.Forwarder;

using ICP.Models.Icp;

using ICP.Services;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Localization;

using Microsoft.Extensions.Options;

using System.Text.Json;



namespace ICP.Controllers;



public class ForwarderDataUploadController : Controller

{

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

    private readonly ForwarderPendingFileStore _pendingFileStore;

    private readonly ForwarderTableMetadataProvider _tableMetadataProvider;

    private readonly IStringLocalizer<SharedResource> _localizer;

    private readonly ILogger<ForwarderDataUploadController> _logger;



    public ForwarderDataUploadController(

        ApplicationDbContext db,

        IWebHostEnvironment environment,

        IOptions<ForwarderDataUploadOptions> options,

        ForwarderDataImportService importService,

        ForwarderPendingFileStore pendingFileStore,

        ForwarderTableMetadataProvider tableMetadataProvider,

        IStringLocalizer<SharedResource> localizer,

        ILogger<ForwarderDataUploadController> logger)

    {

        _db = db;

        _environment = environment;

        _options = options.Value;

        _importService = importService;

        _pendingFileStore = pendingFileStore;

        _tableMetadataProvider = tableMetadataProvider;

        _localizer = localizer;

        _logger = logger;

    }



    [HttpGet]

    public IActionResult Index()

    {

        ViewData["MaxSizeMb"] = _options.MaxSizeMb;

        var tableConfig = _tableMetadataProvider.GetPageConfig();

        ViewData["ForwarderTableConfigJson"] = JsonSerializer.Serialize(new

        {

            filterFieldMap = tableConfig.FilterFieldMap,

            initialSortColumn = tableConfig.ResolveInitialSortColumnIndex() ?? 0,

            initialSortDirection = string.IsNullOrWhiteSpace(tableConfig.InitialSort?.Direction)

                ? "asc"

                : tableConfig.InitialSort!.Direction,

            stickyHeader = tableConfig.TableUi.StickyHeader == true,

            stickyLeftColumns = tableConfig.TableUi.StickyLeftColumns == true

        });

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

        [FromForm] ForwarderDataUploadSearchModel criteria,

        CancellationToken cancellationToken = default)

    {

        if (string.IsNullOrWhiteSpace(criteria.FilePath))

        {

            return PartialView("~/Views/FORWARDER/ForwarderDataUpload/View.List.cshtml", CreateListViewModel([]));

        }



        var tableConfig = _tableMetadataProvider.GetPageConfig();

        var normalizedPath = ForwarderDataImportService.ValidateAndNormalizeStoredFilePath(

            criteria.FilePath,

            _environment,

            _options);



        var rows = await LoadRowsAsync(normalizedPath, criteria.Preview, cancellationToken);



        var viewRows = await _importService.BuildRowViewModelsAsync(rows, criteria.Preview, cancellationToken);

        viewRows = ForwarderDataImportService.ApplyDuplicateStatusFilter(viewRows, criteria.DuplicateStatuses);

        var columnFilters = ForwarderSearchFilterHelper.ParseColumnFilters(Request.Form, tableConfig.Fields);

        viewRows = ForwarderSearchFilterHelper.ApplyColumnFilters(viewRows, columnFilters);



        return PartialView("~/Views/FORWARDER/ForwarderDataUpload/View.List.cshtml", CreateListViewModel(viewRows));

    }



    [HttpGet]

    public async Task<IActionResult> GetFilterOptions(

        string column,

        string? filePath,

        bool preview = true,

        string? search = null,

        CancellationToken cancellationToken = default)

    {

        if (!_tableMetadataProvider.IsSearchableColumn(column))

        {

            _logger.LogWarning("Filter options requested for non-searchable column: {Column}", column);

            return BadRequest();

        }



        if (string.Equals(column, "DuplicateStatus", StringComparison.OrdinalIgnoreCase))

        {

            return Json(new[] { "DbDuplicate", "InFileMultiLine", "None" });

        }



        var tableConfig = _tableMetadataProvider.GetPageConfig();

        var field = tableConfig.Fields.FirstOrDefault(item =>

            string.Equals(item.FieldName, column, StringComparison.OrdinalIgnoreCase));

        if (field is null || !ForwarderSearchFilterHelper.IsCheckboxFilter(field))

        {

            _logger.LogWarning("Filter options not implemented for column filter type: {Column}", column);

            return BadRequest();

        }



        if (string.IsNullOrWhiteSpace(filePath))

        {

            return Json(Array.Empty<string>());

        }



        var normalizedPath = ForwarderDataImportService.ValidateAndNormalizeStoredFilePath(

            filePath,

            _environment,

            _options);



        var rows = await LoadRowsAsync(normalizedPath, preview, cancellationToken);

        var viewRows = await _importService.BuildRowViewModelsAsync(rows, preview, cancellationToken);

        var options = ForwarderSearchFilterHelper.GetDistinctValues(viewRows, column, search);

        return Json(options);

    }



    private async Task<List<ForwarderDataUpload>> LoadRowsAsync(

        string normalizedPath,

        bool preview,

        CancellationToken cancellationToken)

    {

        if (preview)

        {

            var createUser = CrudAuditHelper.ResolveUserName(User.Identity?.Name);

            return await _importService.ParseAsync(normalizedPath, createUser, cancellationToken);

        }



        return await _db.ForwarderDataUploads

            .AsNoTracking()

            .Where(x => x.FilePath == normalizedPath)

            .OrderBy(x => x.Id)

            .ToListAsync(cancellationToken);

    }



    private ForwarderDataUploadListViewModel CreateListViewModel(

        IReadOnlyList<ForwarderDataUploadRowViewModel> listData)

    {

        var tableConfig = _tableMetadataProvider.GetPageConfig();

        return new ForwarderDataUploadListViewModel

        {

            ListData = listData,

            Fields = tableConfig.Fields,

            TableUi = tableConfig.TableUi,

            HasFilterRow = tableConfig.HasFilterRow

        };

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

            var duplicateInvoiceNos = await _importService.GetDuplicateInvoiceNumbersAsync(

                rows.Select(row => row.InvoiceNo),

                cancellationToken);

            _pendingFileStore.Add(HttpContext, storedPath);



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

                fileName = safeFileName,

                duplicateInvoiceNos,

                hasDuplicateInvoices = duplicateInvoiceNos.Count > 0

            });

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Forwarder data preview parse failed: {FileName}", safeFileName);



            if (System.IO.File.Exists(storedPath))

            {

                System.IO.File.Delete(storedPath);

            }



            _pendingFileStore.Remove(HttpContext, storedPath);

            return Json(new { success = false, message = ex.Message });

        }

    }



    [HttpPost]

    public async Task<IActionResult> Save(

        [FromForm] string? filePath,

        [FromForm] bool confirmOverwrite = false,

        CancellationToken cancellationToken = default)

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



            if (!_pendingFileStore.Contains(HttpContext, normalizedPath))

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

            var result = await _importService.SaveAsync(normalizedPath, createUser, confirmOverwrite, cancellationToken);



            if (result.RequiresOverwriteConfirmation)

            {

                return Json(new

                {

                    success = false,

                    requiresConfirmation = true,

                    duplicateInvoiceNos = result.DuplicateInvoiceNos,

                    message = _localizer["Forwarder.ForwarderDataUpload.DuplicateInvoiceConfirm"].Value

                });

            }



            if (!result.Success)

            {

                return Json(new { success = false, message = result.Message });

            }



            _pendingFileStore.Remove(HttpContext, normalizedPath);



            _logger.LogInformation(

                "Forwarder data saved: {StoredPath}, Count={Count}",

                normalizedPath,

                result.ImportedCount);



            var message = string.Format(

                _localizer["Forwarder.ForwarderDataUpload.SaveSuccessMessage"].Value,

                result.ImportedCount);



            if (result.OverwrittenCount > 0)

            {

                message += " " + string.Format(

                    _localizer["Forwarder.ForwarderDataUpload.OverwriteArchivedMessage"].Value,

                    result.OverwrittenCount);

            }



            return Json(new

            {

                success = result.Success,

                message,

                importedCount = result.ImportedCount,

                overwrittenCount = result.OverwrittenCount,

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



            if (_pendingFileStore.Contains(HttpContext, normalizedPath))

            {

                _pendingFileStore.Remove(HttpContext, normalizedPath);

            }

            else

            {

                _logger.LogWarning(

                    "Forwarder pending file cancel: not in pending store {StoredPath}",

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

}

