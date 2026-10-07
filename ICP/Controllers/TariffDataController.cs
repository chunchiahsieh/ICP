using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using ICP.Models.Tariff;
using ICP.Services;
using System.Text.Json;
using System.Globalization;
using System.Data.Common;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ICP.Controllers;

public class TariffDataController : Controller
{
    private const string TemplateFileName = "KWE_TariffCustomsDataTemplate.xls";
    private const string TemplateDownloadFileName = "CustomsDataUploadTemplate.xls";

    private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls" };
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf" };

    private const string PdfAttachmentType = "pdf";
    private const string CostAttachmentType = "cost";

    private readonly ApplicationDbContext _db;
    private readonly PageDataScopeService _scope;
    private readonly IWebHostEnvironment _environment;
    private readonly TariffDataOptions _options;
    private readonly TariffDataImportService _importService;
    private readonly TariffTableMetadataProvider _tableMetadataProvider;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<TariffDataController> _logger;
    private readonly UserAuthService _userAuthService;
    private readonly UserResourcePermissionService _permissionService;

    public TariffDataController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        IOptions<TariffDataOptions> options,
        TariffDataImportService importService,
        TariffTableMetadataProvider tableMetadataProvider,
        IStringLocalizer<SharedResource> localizer,
        ILogger<TariffDataController> logger,
        PageDataScopeService scope,
        UserAuthService userAuthService,
        UserResourcePermissionService permissionService)
    {
        _db = db;
        _scope = scope;
        _environment = environment;
        _options = options.Value;
        _importService = importService;
        _tableMetadataProvider = tableMetadataProvider;
        _localizer = localizer;
        _logger = logger;
        _userAuthService = userAuthService;
        _permissionService = permissionService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["MaxSizeMb"] = _options.MaxSizeMb;
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        ViewData["TariffTableConfigJson"] = JsonSerializer.Serialize(new
        {
            fields = tableConfig.Fields.Select(field => new
            {
                fieldName = field.FieldName,
                visible = field.Visible,
                searchable = field.Searchable,
                filterType = field.FilterType
            }),
            initialSort = tableConfig.ResolveInitialSortColumns(),
            initialSortColumn = tableConfig.ResolveInitialSortColumnIndex() ?? 0,
            initialSortDirection = string.IsNullOrWhiteSpace(tableConfig.InitialSort?.Direction)
                ? "desc"
                : tableConfig.InitialSort!.Direction,
            stickyHeader = tableConfig.TableUi.StickyHeader == true,
            stickyLeftColumns = tableConfig.TableUi.StickyLeftColumns == true
        });
        return View("~/Views/BROKER/TariffData/View.cshtml");
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
            "application/vnd.ms-excel",
            TemplateDownloadFileName);
    }

    [HttpPost]
    public async Task<IActionResult> Query(
        [FromForm] TariffDataQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var list = await QueryTariffDataAsync(criteria, cancellationToken);
        return PartialView("~/Views/BROKER/TariffData/View.List.cshtml", CreateListViewModel(list));
    }

    private bool CanEditLogRemarks() =>
        _permissionService.HasPermission(TariffDataPermissionCodes.View)
        && _permissionService.HasPermission(TariffDataPermissionCodes.Edit);

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetLogRemarks(long id, CancellationToken cancellationToken = default)
    {
        if (!CanEditLogRemarks())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, message = _localizer["Broker.TariffData.LogRemarks.Forbidden"].Value });

        var row = await BaseQuery().Where(item => item.Id == id)
            .Select(item => new { item.Id, item.InvoiceNumber, item.LOGRemarks })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null)
            return NotFound(new { success = false, message = _localizer["Broker.TariffData.LogRemarks.NotFound"].Value });

        return Json(new
        {
            success = true,
            id = row.Id.ToString(CultureInfo.InvariantCulture),
            invoiceNumber = row.InvoiceNumber,
            logRemarks = row.LOGRemarks ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveLogRemarks(
        [FromForm] TariffLogRemarksEditModel request,
        CancellationToken cancellationToken = default)
    {
        if (!CanEditLogRemarks())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, message = _localizer["Broker.TariffData.LogRemarks.Forbidden"].Value });

        if ((request.LOGRemarks?.Length ?? 0) > TariffData.LogRemarksMaxLength
            || (request.OriginalLOGRemarks?.Length ?? 0) > TariffData.LogRemarksMaxLength)
            return BadRequest(new { success = false, message = _localizer["Broker.TariffData.LogRemarks.TooLong"].Value });

        if (!ModelState.IsValid || request.Id <= 0)
            return BadRequest(new { success = false, message = _localizer["Broker.TariffData.LogRemarks.SaveFailed"].Value });

        var original = request.OriginalLOGRemarks ?? string.Empty;
        var remarks = string.IsNullOrEmpty(request.LOGRemarks) ? null : request.LOGRemarks;
        var user = CrudAuditHelper.ResolveUserName(User.Identity?.Name);
        if (user.Length > 50) user = user[..50];
        var updatedAt = DateTime.Now;

        try
        {
            // Update only the remarks/audit fields and only within the current user's data scope.
            // Binary comparison plus byte length prevents overwriting stale case/whitespace edits.
            var rows = _scope.Apply(_db.TariffDataRecords).Where(item => item.Id == request.Id);
            var updated = await rows.Where(item =>
                    EF.Functions.Collate(item.LOGRemarks ?? string.Empty, "Latin1_General_100_BIN2") == original
                    && EF.Functions.DataLength(item.LOGRemarks ?? string.Empty) == original.Length * 2)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LOGRemarks, remarks)
                    .SetProperty(item => item.UpdateTime, updatedAt)
                    .SetProperty(item => item.UpdateUser, user), cancellationToken);

            if (updated == 0)
            {
                if (!await rows.AnyAsync(cancellationToken))
                    return NotFound(new { success = false, message = _localizer["Broker.TariffData.LogRemarks.NotFound"].Value });

                return Conflict(new { success = false, message = _localizer["Broker.TariffData.LogRemarks.Conflict"].Value });
            }

            _logger.LogInformation("Tariff LOGRemarks updated: Id={Id}, User={User}", request.Id, user);
            return Json(new { success = true, message = _localizer["Broker.TariffData.LogRemarks.SaveSuccess"].Value });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Tariff LOGRemarks update failed: Id={Id}", request.Id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { success = false, message = _localizer["Broker.TariffData.LogRemarks.SaveFailed"].Value });
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DownloadExcel(
        [FromForm] TariffDataQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        var list = await QueryTariffDataAsync(criteria, cancellationToken);
        var storageRoot = TariffAttachmentHelper.ResolveStorageRoot(_environment, _options);

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("TariffData");

        var exportFields = tableConfig.Fields
            .Where(field => !string.Equals(field.FieldName, "Actions", StringComparison.OrdinalIgnoreCase))
            .ToList();

        for (var columnIndex = 0; columnIndex < exportFields.Count; columnIndex++)
        {
            var field = exportFields[columnIndex];
            worksheet.Cell(1, columnIndex + 1).Value = TariffTableViewHelper.ResolveHeaderLabel(
                field,
                key => _localizer[key].Value);
        }

        var rowIndex = 2;
        foreach (var item in list)
        {
            for (var columnIndex = 0; columnIndex < exportFields.Count; columnIndex++)
            {
                var fieldName = exportFields[columnIndex].FieldName;
                worksheet.Cell(rowIndex, columnIndex + 1).Value = ResolveExportCellValue(
                    item,
                    fieldName,
                    rowIndex - 1,
                    storageRoot);
            }

            rowIndex++;
        }

        var usedRange = worksheet.RangeUsed();
        if (usedRange is not null)
        {
            var header = worksheet.Range(1, 1, 1, exportFields.Count);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;
            usedRange.SetAutoFilter();
            worksheet.Columns().AdjustToContents(1, 50);
        }

        worksheet.SheetView.FreezeRows(1);

        _logger.LogInformation(
            "Tariff data exported by {User}: {Count} record(s)",
            CrudAuditHelper.ResolveUserName(User.Identity?.Name),
            list.Count);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"TariffData_{DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture)}.xlsx";
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpGet]
    public async Task<IActionResult> GetFilterOptions(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column) || !_tableMetadataProvider.IsCheckboxFilterColumn(column))
        {
            return BadRequest();
        }

        if (TariffMetadataHelper.IsAttachmentPresenceField(column))
        {
            return Json(GetAttachmentPresenceFilterOptions(column, search));
        }

        var options = await GetDistinctColumnValuesAsync(column, search, cancellationToken);
        return Json(options);
    }

    [HttpPost]
    [RequestSizeLimit(57_671_680)]
    public async Task<IActionResult> UploadCustomsData(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!_permissionService.HasPermission(TariffDataPermissionCodes.View)
            || !_permissionService.HasPermission("Views.Broker.TariffData.UploadCustomsData"))
        {
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, message = _localizer["Permission.AccessDenied"].Value });
        }

        if (file is null || file.Length == 0)
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.NoFileSelected"].Value });
        }

        if (file.Length > _options.MaxSizeBytes)
        {
            return Json(new
            {
                success = false,
                message = string.Format(_localizer["Broker.TariffData.MaxSizeExceeded"].Value, _options.MaxSizeMb)
            });
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !ExcelExtensions.Contains(extension))
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.InvalidFileType"].Value });
        }

        var safeFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.InvalidFileName"].Value });
        }

        var uploadDirectory = ResolveStorageDirectory("customs");
        Directory.CreateDirectory(uploadDirectory);

        var storedFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeFileName}";
        var storedPath = Path.GetFullPath(Path.Combine(uploadDirectory, storedFileName));

        try
        {
            await using (var stream = System.IO.File.Create(storedPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var sessionTelId = _userAuthService.GetSessionUserInfo()?.TelId;
            var createUser = CrudAuditHelper.ResolveUserName(
                string.IsNullOrWhiteSpace(sessionTelId) ? User.Identity?.Name : sessionTelId.Trim());
            var importResult = await _importService.ImportCustomsDataAsync(
                storedPath,
                safeFileName,
                createUser,
                cancellationToken);

            _logger.LogInformation(
                "Tariff customs data imported: {FileName} -> {StoredPath}, inserted {Inserted}, updated {Updated}, User={User}",
                safeFileName,
                storedPath,
                importResult.ImportedCount,
                importResult.UpdatedCount,
                createUser);

            var message = string.Format(
                _localizer["Broker.TariffData.UploadCustomsDataSuccess"].Value,
                safeFileName,
                importResult.TotalCount);

            return Json(new
            {
                success = true,
                message,
                filePath = storedPath,
                importedCount = importResult.ImportedCount,
                updatedCount = importResult.UpdatedCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tariff customs data upload failed: {FileName}", safeFileName);

            if (System.IO.File.Exists(storedPath))
            {
                System.IO.File.Delete(storedPath);
            }

            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ListAttachments(
        string hawb,
        string kind,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAttachmentRequest(hawb, kind);
        if (normalized is null || !await CanAccessHawbAsync(normalized.Value.Hawb, cancellationToken))
        {
            return NotFound(new { success = false, message = _localizer["Broker.TariffData.HawbNotFound", hawb].Value });
        }

        var files = await GetAttachmentFilesAsync(normalized.Value.Hawb, normalized.Value.Kind, cancellationToken);
        return Json(new
        {
            success = true,
            files = files.Select(file => new
            {
                originalFileName = file.Info.Name,
                fileSize = file.Info.Length,
                createTime = file.Info.LastWriteTime,
                createUser = string.Empty,
                isCurrent = file.IsCurrent
            })
        });
    }

    [HttpPost]
    [RequestSizeLimit(57_671_680)]
    public Task<IActionResult> UploadDeclarationPdf(
        IFormFile? file,
        CancellationToken cancellationToken = default) =>
        UploadHawbAttachmentAsync(file, PdfAttachmentType, cancellationToken);

    [HttpPost]
    [RequestSizeLimit(57_671_680)]
    public Task<IActionResult> UploadCost(
        IFormFile? file,
        CancellationToken cancellationToken = default) =>
        UploadHawbAttachmentAsync(file, CostAttachmentType, cancellationToken);

    private async Task<IActionResult> UploadHawbAttachmentAsync(
        IFormFile? file,
        string kind,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.NoFileSelected"].Value });
        }

        if (file.Length > _options.MaxSizeBytes)
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.MaxSizeExceeded", _options.MaxSizeMb].Value });
        }

        var originalName = Path.GetFileName(file.FileName);
        var extension = Path.GetExtension(originalName);
        var allowed = kind == PdfAttachmentType ? PdfExtensions : ExcelExtensions;
        if (string.IsNullOrWhiteSpace(originalName) || !allowed.Contains(extension))
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.InvalidFileType"].Value });
        }

        var hawb = Path.GetFileNameWithoutExtension(originalName).Trim();
        if (string.IsNullOrWhiteSpace(hawb) || !await CanAccessHawbAsync(hawb, cancellationToken))
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.HawbNotFound", hawb].Value });
        }

        var matchingRows = await _scope.Apply(_db.TariffDataRecords)
            .Where(item => item.HAWB == hawb)
            .ToListAsync(cancellationToken);
        if (matchingRows.Count == 0)
            return Json(new { success = false, message = _localizer["Broker.TariffData.HawbNotFound", hawb].Value });
        var subFolder = kind == PdfAttachmentType
            ? TariffAttachmentHelper.DeclarationPdfFolder
            : TariffAttachmentHelper.CostFolder;
        var uploadDirectory = ResolveStorageDirectory(subFolder);
        Directory.CreateDirectory(uploadDirectory);
        var storedName = TariffAttachmentHelper.SanitizeHawbFileStem(hawb) + extension.ToLowerInvariant();
        var targetPath = Path.GetFullPath(Path.Combine(uploadDirectory, storedName));
        var storageRoot = TariffAttachmentHelper.ResolveStorageRoot(_environment, _options);
        var existingPath = kind == PdfAttachmentType
            ? TariffAttachmentHelper.FindDeclarationPdfPath(storageRoot, matchingRows[0])
            : TariffAttachmentHelper.FindCostFilePath(storageRoot, matchingRows[0]);
        string? versionPath = null;
        try
        {
            if (existingPath is not null && System.IO.File.Exists(existingPath))
            {
                var existingExtension = Path.GetExtension(existingPath).ToLowerInvariant();
                versionPath = Path.Combine(uploadDirectory,
                    $"{TariffAttachmentHelper.SanitizeHawbFileStem(hawb)}__v{DateTime.Now:yyyyMMddHHmmssfff}{existingExtension}");
                System.IO.File.Move(existingPath, versionPath);
            }
            await using (var stream = System.IO.File.Create(targetPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }
            if (kind == PdfAttachmentType)
            {
                var relativePath = $"{TariffAttachmentHelper.DeclarationPdfFolder}/{storedName}";
                foreach (var row in matchingRows) row.DeclarationFile = relativePath;
            }
            else
            {
                foreach (var row in matchingRows) row.Cost = storedName;
            }
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            if (System.IO.File.Exists(targetPath)) System.IO.File.Delete(targetPath);
            if (versionPath is not null && existingPath is not null && System.IO.File.Exists(versionPath))
                System.IO.File.Move(versionPath, existingPath);
            _logger.LogError(ex, "Tariff HAWB attachment upload failed: {Kind} {FileName}", kind, originalName);
            return Json(new { success = false, message = ex.Message });
        }

        var successKey = kind == PdfAttachmentType
            ? "Broker.TariffData.UploadDeclarationPdfSuccess"
            : "Broker.TariffData.UploadCostSuccess";
        return Json(new { success = true, message = string.Format(_localizer[successKey].Value, storedName), filePath = targetPath, hawb });
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(
        string kind,
        string hawb,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAttachmentFileAsync(hawb, kind, fileName, cancellationToken);
        return resolved is null ? NotFound() : PhysicalFile(resolved.Value.Path, ResolveContentType(Path.GetExtension(resolved.Value.Path)), Path.GetFileName(resolved.Value.Path));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRecord(string? invoiceNumber, CancellationToken cancellationToken = default)
    {
        if (!_permissionService.HasPermission(TariffDataPermissionCodes.View)
            || !_permissionService.HasPermission("Views.Broker.TariffData.Delete"))
            return StatusCode(StatusCodes.Status403Forbidden,
                new { success = false, message = _localizer["Permission.AccessDenied"].Value });

        invoiceNumber = invoiceNumber?.Trim();
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            return BadRequest(new { success = false, message = _localizer["Broker.TariffData.Delete.NotFound"].Value });

        await using var transaction = await _db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken);
        var rows = await _db.TariffDataRecords
            .Where(item => item.InvoiceNumber == invoiceNumber)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return NotFound(new { success = false, message = _localizer["Broker.TariffData.Delete.NotFound"].Value });
        }

        _db.TariffDataRecords.RemoveRange(rows);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        _logger.LogInformation("Tariff data deleted by invoice: InvoiceNumber={InvoiceNumber}, Rows={Count}, User={User}",
            invoiceNumber, rows.Count, CrudAuditHelper.ResolveUserName(User.Identity?.Name));
        return Json(new { success = true, message = _localizer["Broker.TariffData.Delete.RecordSuccess"].Value });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(
        string hawb,
        string kind,
        string? fileName = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveAttachmentFileAsync(hawb, kind, fileName, cancellationToken);
        if (resolved is null)
            return NotFound(new { success = false, message = _localizer["Broker.TariffData.Delete.NotFound"].Value });

        var storageRoot = TariffAttachmentHelper.ResolveStorageRoot(_environment, _options);
        var recycleFolder = Path.Combine(storageRoot, ".deleted", resolved.Value.Kind);
        Directory.CreateDirectory(recycleFolder);
        var recyclePath = Path.Combine(recycleFolder, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Path.GetFileName(resolved.Value.Path)}");
        System.IO.File.Move(resolved.Value.Path, recyclePath);
        string? promotedFrom = null;
        string? promotedTo = null;
        try
        {
            if (resolved.Value.IsCurrent)
            {
                var remaining = await GetAttachmentFilesAsync(resolved.Value.Hawb, resolved.Value.Kind, cancellationToken);
                var latestVersion = remaining.FirstOrDefault(file => !file.IsCurrent);
                var matchingRows = await _scope.Apply(_db.TariffDataRecords)
                    .Where(item => item.HAWB == resolved.Value.Hawb)
                    .ToListAsync(cancellationToken);
                if (latestVersion.Info is not null)
                {
                    var extension = latestVersion.Info.Extension.ToLowerInvariant();
                    var currentName = TariffAttachmentHelper.SanitizeHawbFileStem(resolved.Value.Hawb) + extension;
                    var currentPath = Path.Combine(latestVersion.Info.DirectoryName!, currentName);
                    promotedFrom = latestVersion.Info.FullName;
                    promotedTo = currentPath;
                    System.IO.File.Move(latestVersion.Info.FullName, currentPath);
                    foreach (var row in matchingRows)
                    {
                        if (resolved.Value.Kind == PdfAttachmentType)
                            row.DeclarationFile = $"{TariffAttachmentHelper.DeclarationPdfFolder}/{currentName}";
                        else row.Cost = currentName;
                    }
                }
                else
                {
                    foreach (var row in matchingRows)
                    {
                        if (resolved.Value.Kind == PdfAttachmentType) row.DeclarationFile = null;
                        else row.Cost = null;
                    }
                }
                await _db.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            if (promotedFrom is not null && promotedTo is not null && System.IO.File.Exists(promotedTo))
                System.IO.File.Move(promotedTo, promotedFrom);
            if (System.IO.File.Exists(recyclePath)) System.IO.File.Move(recyclePath, resolved.Value.Path);
            throw;
        }
        _logger.LogInformation("Tariff attachment deleted: HAWB={Hawb}, Kind={Kind}, RecyclePath={RecyclePath}, User={User}",
            resolved.Value.Hawb, resolved.Value.Kind, recyclePath, CrudAuditHelper.ResolveUserName(User.Identity?.Name));
        return Json(new { success = true, message = _localizer["Broker.TariffData.Delete.AttachmentSuccess"].Value });
    }

    private TariffDataSearchListViewModel CreateListViewModel(IReadOnlyList<TariffData> listData)
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        return new TariffDataSearchListViewModel
        {
            ListData = listData,
            Fields = tableConfig.Fields,
            TableUi = tableConfig.TableUi,
            HasFilterRow = tableConfig.HasFilterRow,
            CanEditLogRemarks = CanEditLogRemarks(),
            StorageRoot = TariffAttachmentHelper.ResolveStorageRoot(_environment, _options)
        };
    }

    private IQueryable<TariffData> BaseQuery()
    {
        return _scope.Apply(_db.TariffDataRecords).AsNoTracking();
    }

    private string ResolveExportCellValue(
        TariffData item,
        string fieldName,
        int rowNumber,
        string storageRoot)
    {
        if (string.Equals(fieldName, "RowNo", StringComparison.OrdinalIgnoreCase))
        {
            return rowNumber.ToString(CultureInfo.InvariantCulture);
        }

        if (string.Equals(fieldName, "DeclarationPdf", StringComparison.OrdinalIgnoreCase))
        {
            return TariffAttachmentHelper.FindDeclarationPdfPath(storageRoot, item) is null
                ? _localizer["Broker.TariffData.Export.No"].Value
                : _localizer["Broker.TariffData.Export.Yes"].Value;
        }

        if (string.Equals(fieldName, "CostFile", StringComparison.OrdinalIgnoreCase))
        {
            return TariffAttachmentHelper.FindCostFilePath(storageRoot, item) is null
                ? _localizer["Broker.TariffData.Export.No"].Value
                : _localizer["Broker.TariffData.Export.Yes"].Value;
        }

        return TariffTableViewHelper.FormatCellValue(item, fieldName);
    }

    private async Task<List<TariffData>> QueryTariffDataAsync(
        TariffDataQueryModel criteria,
        CancellationToken cancellationToken)
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        var query = TariffQueryFilterApplier.ApplyFilters(BaseQuery(), criteria, tableConfig.Fields,
            deferCreateUserTextFilter: true);
        query = await ApplyAttachmentPresenceFiltersAsync(query, criteria, tableConfig.Fields, cancellationToken);

        var list = await query
            .OrderByDescending(e => e.Id)
            .ToListAsync(cancellationToken);
        if (list.Count > 0 && tableConfig.Fields.Any(field =>
            string.Equals(field.FieldName, nameof(TariffData.CreateUser), StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                var displayNames = await _userAuthService.GetUserDisplayNamesAsync(list.Select(row => row.CreateUser), cancellationToken);
                foreach (var row in list)
                    row.CreateUserDisplayName = UserDisplayNameHelper.ResolveDisplayName(row.CreateUser, displayNames);
            }
            catch (Exception ex) when (ex is DbException or TimeoutException)
            {
                // Directory availability must not prevent viewing customs data. Retain raw audit accounts.
                _logger.LogWarning(ex, "Tariff creator display names could not be loaded; using stored accounts.");
            }
        }

        list = TariffQueryFilterApplier.ApplyCreateUserTextFilter(list, criteria, tableConfig.Fields);
        return TariffTableSortHelper.Apply(list, tableConfig);
    }

    private async Task<IQueryable<TariffData>> ApplyAttachmentPresenceFiltersAsync(
        IQueryable<TariffData> query,
        TariffDataQueryModel criteria,
        IReadOnlyList<TariffTableFieldMetadata> fields,
        CancellationToken cancellationToken)
    {
        var storageRoot = TariffAttachmentHelper.ResolveStorageRoot(_environment, _options);

        if (TryGetAttachmentCheckboxValues(criteria, fields, "DeclarationPdf", out var pdfValues))
        {
            var dbRows = await _scope.Apply(_db.TariffDataRecords)
                .AsNoTracking()
                .Where(e => e.DeclarationFile != null && e.DeclarationFile != "")
                .Select(e => new { e.HAWB, e.DeclarationFile })
                .Distinct()
                .ToListAsync(cancellationToken);

            var hawbsWithPdf = TariffAttachmentHelper.CollectHawbsWithDeclarationPdf(
                storageRoot,
                dbRows.Select(r => (r.HAWB, (string?)r.DeclarationFile)));

            query = TariffAttachmentHelper.ApplyPresenceFilter(query, pdfValues, hawbsWithPdf);
        }

        if (TryGetAttachmentCheckboxValues(criteria, fields, "CostFile", out var costValues))
        {
            var dbRows = await _scope.Apply(_db.TariffDataRecords)
                .AsNoTracking()
                .Where(e => e.Cost != null && e.Cost != "")
                .Select(e => new { e.HAWB, e.Cost })
                .Distinct()
                .ToListAsync(cancellationToken);

            var hawbsWithCost = TariffAttachmentHelper.CollectHawbsWithCost(
                storageRoot,
                dbRows.Select(r => (r.HAWB, (string?)r.Cost)));

            query = TariffAttachmentHelper.ApplyPresenceFilter(query, costValues, hawbsWithCost);
        }

        return query;
    }

    private static bool TryGetAttachmentCheckboxValues(
        TariffDataQueryModel criteria,
        IReadOnlyList<TariffTableFieldMetadata> fields,
        string fieldName,
        out List<string> values)
    {
        values = [];
        var meta = fields.FirstOrDefault(field =>
            string.Equals(field.FieldName, fieldName, StringComparison.OrdinalIgnoreCase));
        if (meta is null
            || !meta.Searchable
            || !TariffMetadataHelper.IsCheckboxFilter(meta)
            || !TariffMetadataHelper.IsAttachmentPresenceField(fieldName))
        {
            return false;
        }

        var selected = criteria.Checkbox
            .FirstOrDefault(pair => string.Equals(pair.Key, fieldName, StringComparison.OrdinalIgnoreCase))
            .Value;
        if (selected is null || selected.Count == 0)
        {
            return false;
        }

        values = selected;
        return true;
    }

    private List<object> GetAttachmentPresenceFilterOptions(string column, string? search)
    {
        var isPdf = string.Equals(column, "DeclarationPdf", StringComparison.OrdinalIgnoreCase);
        var options = new (string value, string label)[]
        {
            (
                TariffAttachmentHelper.PresenceHas,
                isPdf
                    ? _localizer["Broker.TariffData.Filter.HasPdf"].Value
                    : _localizer["Broker.TariffData.Filter.HasCost"].Value
            ),
            (
                TariffAttachmentHelper.PresenceNone,
                isPdf
                    ? _localizer["Broker.TariffData.Filter.NonePdf"].Value
                    : _localizer["Broker.TariffData.Filter.NoneCost"].Value
            )
        };

        IEnumerable<(string value, string label)> filtered = options;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            filtered = options.Where(option =>
                option.label.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        return filtered
            .Select(option => (object)new { option.value, option.label })
            .ToList();
    }

    private async Task<List<string>> GetDistinctColumnValuesAsync(
        string column,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        return column switch
        {
            "MAWB" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.MAWB), search, cancellationToken),
            "HAWB" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.HAWB), search, cancellationToken),
            "ImportDate" => await SearchFilterHelper.DistinctDateOnlyAsync(query.Select(e => e.ImportDate), search, cancellationToken),
            "DeclarationDate" => await SearchFilterHelper.DistinctDateOnlyAsync(query.Select(e => e.DeclarationDate), search, cancellationToken),
            "ReleaseDate" => await SearchFilterHelper.DistinctDateOnlyAsync(query.Select(e => e.ReleaseDate), search, cancellationToken),
            "InvoiceNumber" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.InvoiceNumber), search, cancellationToken),
            "DescriptionOfGoods" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.DescriptionOfGoods), search, cancellationToken),
            "HTSNumber" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.HTSNumber), search, cancellationToken),
            "EntryNumber" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.EntryNumber), search, cancellationToken),
            "Mode" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Mode), search, cancellationToken),
            "PortOfDeparture" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.PortOfDeparture), search, cancellationToken),
            "FlightNo" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.FlightNo), search, cancellationToken),
            "Shipper" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Shipper), search, cancellationToken),
            "Broker" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Broker), search, cancellationToken),
            "AirSea" => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.AirSea), search, cancellationToken),
            "CreateDate" => await SearchFilterHelper.DistinctDateOnlyAsync(query.Select(e => e.CreateDate), search, cancellationToken),
            _ => []
        };
    }

    private static (string Hawb, string Kind)? NormalizeAttachmentRequest(string? hawb, string? kind)
    {
        var normalizedHawb = hawb?.Trim();
        var normalizedKind = kind?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalizedHawb)
            || normalizedKind is not (PdfAttachmentType or CostAttachmentType)
            ? null
            : (normalizedHawb, normalizedKind);
    }

    private Task<bool> CanAccessHawbAsync(string hawb, CancellationToken cancellationToken) =>
        _scope.Apply(_db.TariffDataRecords).AsNoTracking()
            .AnyAsync(item => item.HAWB == hawb, cancellationToken);

    private async Task<List<(FileInfo Info, bool IsCurrent)>> GetAttachmentFilesAsync(
        string? hawb,
        string? kind,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeAttachmentRequest(hawb, kind);
        if (normalized is null) return [];
        var row = await _scope.Apply(_db.TariffDataRecords).AsNoTracking()
            .FirstOrDefaultAsync(item => item.HAWB == normalized.Value.Hawb, cancellationToken);
        if (row is null) return [];
        var root = TariffAttachmentHelper.ResolveStorageRoot(_environment, _options);
        var currentPath = normalized.Value.Kind == PdfAttachmentType
            ? TariffAttachmentHelper.FindDeclarationPdfPath(root, row)
            : TariffAttachmentHelper.FindCostFilePath(root, row);
        var folder = ResolveStorageDirectory(normalized.Value.Kind == PdfAttachmentType
            ? TariffAttachmentHelper.DeclarationPdfFolder
            : TariffAttachmentHelper.CostFolder);
        var stem = TariffAttachmentHelper.SanitizeHawbFileStem(normalized.Value.Hawb);
        var versions = Directory.Exists(folder)
            ? Directory.EnumerateFiles(folder, $"{stem}__v*.*")
                .Where(path => (normalized.Value.Kind == PdfAttachmentType ? PdfExtensions : ExcelExtensions).Contains(Path.GetExtension(path)))
                .Select(path => (Info: new FileInfo(path), IsCurrent: false))
            : [];
        var result = versions.OrderByDescending(item => item.Info.LastWriteTimeUtc).ToList();
        if (currentPath is not null) result.Insert(0, (new FileInfo(currentPath), true));
        return result;
    }

    private async Task<(string Hawb, string Kind, string Path, bool IsCurrent)?> ResolveAttachmentFileAsync(
        string? hawb,
        string? kind,
        string? fileName,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeAttachmentRequest(hawb, kind);
        if (normalized is null) return null;
        var files = await GetAttachmentFilesAsync(normalized.Value.Hawb, normalized.Value.Kind, cancellationToken);
        var selected = string.IsNullOrWhiteSpace(fileName)
            ? files.FirstOrDefault(file => file.IsCurrent)
            : files.FirstOrDefault(file => string.Equals(file.Info.Name, Path.GetFileName(fileName), StringComparison.OrdinalIgnoreCase));
        return selected.Info is null
            ? null
            : (normalized.Value.Hawb, normalized.Value.Kind, selected.Info.FullName, selected.IsCurrent);
    }

    private string ResolveStorageDirectory(string subFolder)
    {
        var root = TariffAttachmentHelper.ResolveStorageRoot(_environment, _options);
        return Path.GetFullPath(Path.Combine(root, subFolder));
    }

    private static string ResolveContentType(string extension) =>
        extension.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".xls" => "application/vnd.ms-excel",
            _ => "application/octet-stream"
        };
}
