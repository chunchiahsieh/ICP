using ClosedXML.Excel;
using ICP.Data;
using Microsoft.AspNetCore.Mvc;
using ICP.Services;
using Microsoft.Extensions.Localization;

namespace ICP.Controllers;

public class CompareIcpVsArUrController(ApplicationDbContext db, IlcDbContext ilc,
    IStringLocalizer<SharedResource> localizer) : Controller
{
    private ArurComparisonService Service => new(db, ilc);

    [HttpGet]
    public IActionResult Index() => View("~/Views/Report/CompareIcpVsArUr/View.cshtml");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Query([FromForm] ArurComparisonQuery query, CancellationToken cancellationToken)
    {
        ArurComparisonPage page;
        try { page = await Service.QueryAsync(query, false, cancellationToken); }
        catch (ArurComparisonDataSourceException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                title: localizer["Compare.DataSourceUnavailable"].Value);
        }
        return Json(new { page.Total, page.Page, page.PageSize, rows = page.Rows.Select(r => new {
            r.Number, r.Result, resultLabel = localizer["Compare." + r.Result].Value, r.RtNo, r.InvoiceNo,
            fieldLabel = FieldLabel(r), r.IcpValue, ilcValue = Actual(r) }) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DownloadExcel([FromForm] ArurComparisonQuery query, CancellationToken cancellationToken)
    {
        ArurComparisonPage data;
        try { data = await Service.QueryAsync(query, true, cancellationToken); }
        catch (ArurComparisonDataSourceException)
        {
            TempData["ErrorMessage"] = localizer["Compare.DataSourceUnavailable"].Value;
            return RedirectToAction(nameof(Index));
        }
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Compare");
        string[] keys = ["Number", "Result", "RtNo", "InvoiceNo", "Field", "IcpValue", "IlcValue"];
        for (var i = 0; i < keys.Length; i++) sheet.Cell(1, i + 1).Value = localizer["Compare." + keys[i]].Value;
        foreach (var row in data.Rows)
        {
            var line = row.Number + 1;
            sheet.Cell(line, 1).Value = row.Number;
            string[] values = [localizer["Compare." + row.Result].Value, row.RtNo, row.InvoiceNo, FieldLabel(row), row.IcpValue, Actual(row)];
            for (var i = 0; i < values.Length; i++) sheet.Cell(line, i + 2).Value = values[i];
            if (row.Result != "Equal") sheet.Range(line, 2, line, 7).Style.Font.FontColor = XLColor.Red;
        }
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, data.Rows.Count + 1, 7).SetAutoFilter();
        sheet.Columns().Width = 24;
        sheet.Columns(5, 7).Width = 45;
        sheet.Style.Alignment.WrapText = true;
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"CompareICPvsARUR_{DateTime.Now:yyyy-MM-dd}.xlsx");
    }

    private string FieldLabel(ArurComparisonRow row) => localizer[row.Field is "Notes" or "ShipTo" ? "Compare.Field." + row.Field : "ShipInfo.Field." + row.Field].Value + " → " + row.TargetField;
    private static string Actual(ArurComparisonRow row) => row.Field is "Eta" or "ArriveTime" ? ArurComparisonService.DisplayDate(row.IlcValue, row.Field == "Eta") : row.IlcValue;
}
