using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.Icp;
using ICP.Models.Tariff;
using ICP.Services;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace ICP.Controllers;

public class TariffDataController : Controller
{
    private const string TemplateFileName = "KWE_TariffCustomsDataTemplate.xls";

    private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls" };
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf" };

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly TariffDataOptions _options;
    private readonly TariffDataImportService _importService;
    private readonly TariffTableMetadataProvider _tableMetadataProvider;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<TariffDataController> _logger;

    public TariffDataController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        IOptions<TariffDataOptions> options,
        TariffDataImportService importService,
        TariffTableMetadataProvider tableMetadataProvider,
        IStringLocalizer<SharedResource> localizer,
        ILogger<TariffDataController> logger)
    {
        _db = db;
        _environment = environment;
        _options = options.Value;
        _importService = importService;
        _tableMetadataProvider = tableMetadataProvider;
        _localizer = localizer;
        _logger = logger;
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
            TemplateFileName);
    }

    [HttpPost]
    public async Task<IActionResult> Query(
        [FromForm] TariffDataQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var list = await QueryTariffDataAsync(criteria, cancellationToken);
        return PartialView("~/Views/BROKER/TariffData/View.List.cshtml", CreateListViewModel(list));
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

        var options = await GetDistinctColumnValuesAsync(column, search, cancellationToken);
        return Json(options);
    }

    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadCustomsData(IFormFile? file, CancellationToken cancellationToken = default)
    {
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

            var createUser = CrudAuditHelper.ResolveUserName(User.Identity?.Name);
            var importResult = await _importService.ImportCustomsDataAsync(
                storedPath,
                safeFileName,
                createUser,
                cancellationToken);

            _logger.LogInformation(
                "Tariff customs data imported: {FileName} -> {StoredPath}, inserted {Inserted}, updated {Updated}",
                safeFileName,
                storedPath,
                importResult.ImportedCount,
                importResult.UpdatedCount);

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

    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadDeclarationPdf(IFormFile? file, CancellationToken cancellationToken = default) =>
        UploadAsync(
            file,
            PdfExtensions,
            "declaration-pdf",
            _localizer["Broker.TariffData.UploadDeclarationPdfSuccess"].Value,
            cancellationToken);

    [HttpPost]
    [RequestSizeLimit(52_428_800)]
    public Task<IActionResult> UploadCost(IFormFile? file, CancellationToken cancellationToken = default) =>
        UploadAsync(
            file,
            PdfExtensions,
            "cost",
            _localizer["Broker.TariffData.UploadCostSuccess"].Value,
            cancellationToken);

    private TariffDataSearchListViewModel CreateListViewModel(IReadOnlyList<TariffData> listData)
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        return new TariffDataSearchListViewModel
        {
            ListData = listData,
            Fields = tableConfig.Fields,
            TableUi = tableConfig.TableUi,
            HasFilterRow = tableConfig.HasFilterRow
        };
    }

    private IQueryable<TariffData> BaseQuery()
    {
        return _db.TariffDataRecords.AsNoTracking();
    }

    private async Task<List<TariffData>> QueryTariffDataAsync(
        TariffDataQueryModel criteria,
        CancellationToken cancellationToken)
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        var query = TariffQueryFilterApplier.ApplyFilters(BaseQuery(), criteria, tableConfig.Fields);

        return await query
            .OrderByDescending(e => e.Id)
            .ToListAsync(cancellationToken);
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

    private async Task<IActionResult> UploadAsync(
        IFormFile? file,
        IReadOnlySet<string> allowedExtensions,
        string subFolder,
        string successMessageTemplate,
        CancellationToken cancellationToken)
    {
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
        if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension))
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.InvalidFileType"].Value });
        }

        var safeFileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(safeFileName))
        {
            return Json(new { success = false, message = _localizer["Broker.TariffData.InvalidFileName"].Value });
        }

        var uploadDirectory = ResolveStorageDirectory(subFolder);
        Directory.CreateDirectory(uploadDirectory);

        var storedFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{safeFileName}";
        var storedPath = Path.GetFullPath(Path.Combine(uploadDirectory, storedFileName));

        try
        {
            await using (var stream = System.IO.File.Create(storedPath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            _logger.LogInformation(
                "Tariff data uploaded: {SubFolder} {FileName} -> {StoredPath}",
                subFolder,
                safeFileName,
                storedPath);

            var message = string.Format(successMessageTemplate, safeFileName);
            return Json(new { success = true, message, filePath = storedPath });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tariff data upload failed: {SubFolder} {FileName}", subFolder, safeFileName);

            if (System.IO.File.Exists(storedPath))
            {
                System.IO.File.Delete(storedPath);
            }

            return Json(new { success = false, message = ex.Message });
        }
    }

    private string ResolveStorageDirectory(string subFolder)
    {
        var root = Path.IsPathRooted(_options.StoragePath)
            ? _options.StoragePath
            : Path.Combine(_environment.ContentRootPath, _options.StoragePath);

        return Path.GetFullPath(Path.Combine(root, subFolder));
    }
}
