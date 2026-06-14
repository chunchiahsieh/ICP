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

public class TariffDataController : Controller
{
    private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xls" };
    private static readonly HashSet<string> PdfExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf" };

    private static readonly HashSet<string> AllowedFilterColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "MAWB",
        "HAWB",
        "ImportDate",
        "DeclarationDate",
        "ReleaseDate",
        "InvoiceNumber",
        "DescriptionOfGoods",
        "HTSNumber",
        "EntryNumber",
        "Mode",
        "PortOfDeparture",
        "FlightNo",
        "Shipper",
        "Broker",
        "AirSea",
        "CreateDate"
    };

    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;
    private readonly TariffDataOptions _options;
    private readonly TariffDataImportService _importService;
    private readonly IStringLocalizer<SharedResource> _localizer;
    private readonly ILogger<TariffDataController> _logger;

    public TariffDataController(
        ApplicationDbContext db,
        IWebHostEnvironment environment,
        IOptions<TariffDataOptions> options,
        TariffDataImportService importService,
        IStringLocalizer<SharedResource> localizer,
        ILogger<TariffDataController> logger)
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
        return View("~/Views/BROKER/TariffData/View.cshtml");
    }

    [HttpPost]
    public async Task<IActionResult> Query(
        [FromForm] TariffDataSearchModel criteria,
        CancellationToken cancellationToken = default)
    {
        var list = await QueryTariffDataAsync(criteria, cancellationToken);
        return PartialView("~/Views/BROKER/TariffData/View.List.cshtml", new TariffDataSearchListViewModel
        {
            ListData = list
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetFilterOptions(
        string column,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(column) || !AllowedFilterColumns.Contains(column))
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

    private IQueryable<TariffData> BaseQuery()
    {
        return _db.TariffDataRecords.AsNoTracking();
    }

    private async Task<List<TariffData>> QueryTariffDataAsync(
        TariffDataSearchModel criteria,
        CancellationToken cancellationToken)
    {
        var query = BaseQuery();

        if (criteria.MAWBs.Count > 0)
        {
            query = query.Where(e => criteria.MAWBs.Contains(e.MAWB));
        }

        if (criteria.HAWBs.Count > 0)
        {
            query = query.Where(e => criteria.HAWBs.Contains(e.HAWB));
        }

        if (criteria.ImportDates.Count > 0)
        {
            var dates = SearchFilterHelper.ParseDateOnlyValues(criteria.ImportDates);
            if (dates.Count > 0)
            {
                query = query.Where(e => dates.Contains(e.ImportDate));
            }
        }

        if (criteria.DeclarationDates.Count > 0)
        {
            var dates = SearchFilterHelper.ParseDateOnlyValues(criteria.DeclarationDates);
            if (dates.Count > 0)
            {
                query = query.Where(e => dates.Contains(e.DeclarationDate));
            }
        }

        if (criteria.ReleaseDates.Count > 0)
        {
            var dates = SearchFilterHelper.ParseDateOnlyValues(criteria.ReleaseDates);
            if (dates.Count > 0)
            {
                query = query.Where(e => dates.Contains(e.ReleaseDate));
            }
        }

        if (criteria.InvoiceNumbers.Count > 0)
        {
            query = query.Where(e => criteria.InvoiceNumbers.Contains(e.InvoiceNumber));
        }

        if (criteria.DescriptionOfGoodsList.Count > 0)
        {
            query = query.Where(e => criteria.DescriptionOfGoodsList.Contains(e.DescriptionOfGoods));
        }

        if (criteria.HTSNumbers.Count > 0)
        {
            query = query.Where(e => criteria.HTSNumbers.Contains(e.HTSNumber));
        }

        if (criteria.EntryNumbers.Count > 0)
        {
            query = query.Where(e => criteria.EntryNumbers.Contains(e.EntryNumber));
        }

        if (criteria.Modes.Count > 0)
        {
            query = query.Where(e => criteria.Modes.Contains(e.Mode));
        }

        if (criteria.PortOfDepartures.Count > 0)
        {
            query = query.Where(e => criteria.PortOfDepartures.Contains(e.PortOfDeparture));
        }

        if (criteria.FlightNos.Count > 0)
        {
            query = query.Where(e => criteria.FlightNos.Contains(e.FlightNo));
        }

        if (criteria.Shippers.Count > 0)
        {
            query = query.Where(e => e.Shipper != null && criteria.Shippers.Contains(e.Shipper));
        }

        if (criteria.Brokers.Count > 0)
        {
            query = query.Where(e => e.Broker != null && criteria.Brokers.Contains(e.Broker));
        }

        if (criteria.AirSeas.Count > 0)
        {
            query = query.Where(e => criteria.AirSeas.Contains(e.AirSea));
        }

        if (criteria.CreateDates.Count > 0)
        {
            var dates = SearchFilterHelper.ParseDateOnlyValues(criteria.CreateDates);
            if (dates.Count > 0)
            {
                query = query.Where(e => dates.Contains(e.CreateDate));
            }
        }

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
