using System.Globalization;
using System.Text.Json;
using ClosedXML.Excel;
using ICP.Data;
using ICP.Helpers;
using ICP.Models;
using ICP.Models.CustomsDataDownload;
using ICP.Models.Icp;
using ICP.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class CustomsDataDownloadController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly CustomsDataDownloadTableMetadataProvider _tableMetadataProvider;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public CustomsDataDownloadController(
        ApplicationDbContext db,
        CustomsDataDownloadTableMetadataProvider tableMetadataProvider,
        IStringLocalizer<SharedResource> localizer)
    {
        _db = db;
        _tableMetadataProvider = tableMetadataProvider;
        _localizer = localizer;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        ViewData["CustomsDataDownloadTableConfigJson"] = JsonSerializer.Serialize(new
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
        return View("~/Views/BROKER/CustomsDataDownload/View.cshtml");
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Query(
        [FromForm] CustomsDataDownloadQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var list = await QueryRowsAsync(criteria, cancellationToken);
        return PartialView("~/Views/BROKER/CustomsDataDownload/View.List.cshtml", CreateListViewModel(list));
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
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> DownloadExcel(
        [FromForm] CustomsDataDownloadQueryModel criteria,
        CancellationToken cancellationToken = default)
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        var list = await QueryRowsAsync(criteria, cancellationToken);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("CustomsData");

        for (var index = 0; index < tableConfig.Fields.Count; index++)
        {
            var field = tableConfig.Fields[index];
            worksheet.Cell(1, index + 1).Value = CustomsDataDownloadTableViewHelper.ResolveHeaderLabel(
                field,
                key => _localizer[key].Value);
        }

        var rowIndex = 2;
        foreach (var item in list)
        {
            for (var index = 0; index < tableConfig.Fields.Count; index++)
            {
                worksheet.Cell(rowIndex, index + 1).Value =
                    CustomsDataDownloadTableViewHelper.FormatCellValue(item, tableConfig.Fields[index].FieldName);
            }

            rowIndex++;
        }

        worksheet.SheetView.FreezeRows(1);
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"CustomsDataDownload_{DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}.xlsx";
        return File(
            stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    private CustomsDataDownloadSearchListViewModel CreateListViewModel(IReadOnlyList<StgRawShippingAdvice> listData)
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        return new CustomsDataDownloadSearchListViewModel
        {
            ListData = listData,
            Fields = tableConfig.Fields,
            TableUi = tableConfig.TableUi,
            HasFilterRow = tableConfig.HasFilterRow
        };
    }

    private IQueryable<StgRawShippingAdvice> BaseQuery() =>
        _db.StgRawShippingAdvice.AsNoTracking();

    private async Task<List<StgRawShippingAdvice>> QueryRowsAsync(
        CustomsDataDownloadQueryModel criteria,
        CancellationToken cancellationToken)
    {
        var tableConfig = _tableMetadataProvider.GetPageConfig();
        var query = CustomsDataDownloadQueryFilterApplier.ApplyFilters(BaseQuery(), criteria, tableConfig.Fields);
        return await query
            .OrderByDescending(e => e.CreatedUtc)
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
            nameof(StgRawShippingAdvice.FileCode) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => (string?)e.FileCode), search, cancellationToken),
            nameof(StgRawShippingAdvice.InvoiceNo) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.InvoiceNo), search, cancellationToken),
            nameof(StgRawShippingAdvice.Forwarder) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Forwarder), search, cancellationToken),
            nameof(StgRawShippingAdvice.Mawb) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Mawb), search, cancellationToken),
            nameof(StgRawShippingAdvice.Hawb) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Hawb), search, cancellationToken),
            nameof(StgRawShippingAdvice.Flt) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Flt), search, cancellationToken),
            nameof(StgRawShippingAdvice.DestinationPort) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.DestinationPort), search, cancellationToken),
            nameof(StgRawShippingAdvice.DestinationCountry) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.DestinationCountry), search, cancellationToken),
            nameof(StgRawShippingAdvice.InvoiceType) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.InvoiceType), search, cancellationToken),
            nameof(StgRawShippingAdvice.Incoterms) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Incoterms), search, cancellationToken),
            nameof(StgRawShippingAdvice.Bu) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Bu), search, cancellationToken),
            nameof(StgRawShippingAdvice.PoNo) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.PoNo), search, cancellationToken),
            nameof(StgRawShippingAdvice.PoLine) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.PoLine), search, cancellationToken),
            nameof(StgRawShippingAdvice.ItemNo) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.ItemNo), search, cancellationToken),
            nameof(StgRawShippingAdvice.Uom) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Uom), search, cancellationToken),
            nameof(StgRawShippingAdvice.Coo) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Coo), search, cancellationToken),
            nameof(StgRawShippingAdvice.Currency) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Currency), search, cancellationToken),
            nameof(StgRawShippingAdvice.PackingType) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.PackingType), search, cancellationToken),
            nameof(StgRawShippingAdvice.NcdrNo) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.NcdrNo), search, cancellationToken),
            nameof(StgRawShippingAdvice.EndUserCode) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.EndUserCode), search, cancellationToken),
            nameof(StgRawShippingAdvice.EndUser) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.EndUser), search, cancellationToken),
            nameof(StgRawShippingAdvice.MachineNo) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.MachineNo), search, cancellationToken),
            nameof(StgRawShippingAdvice.MachineType) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.MachineType), search, cancellationToken),
            nameof(StgRawShippingAdvice.ShipReason) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.ShipReason), search, cancellationToken),
            nameof(StgRawShippingAdvice.DeliveryNo) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.DeliveryNo), search, cancellationToken),
            nameof(StgRawShippingAdvice.SoldToPartyCode) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.SoldToPartyCode), search, cancellationToken),
            nameof(StgRawShippingAdvice.SoldToParty) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.SoldToParty), search, cancellationToken),
            nameof(StgRawShippingAdvice.ShipToPartyCode) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.ShipToPartyCode), search, cancellationToken),
            nameof(StgRawShippingAdvice.ShipToParty) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.ShipToParty), search, cancellationToken),
            nameof(StgRawShippingAdvice.Hazmat) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.Hazmat), search, cancellationToken),
            nameof(StgRawShippingAdvice.WbsElement) => await SearchFilterHelper.DistinctNonEmptyAsync(query.Select(e => e.WbsElement), search, cancellationToken),
            _ => []
        };
    }
}
