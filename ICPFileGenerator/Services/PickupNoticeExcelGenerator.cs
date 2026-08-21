using System.Globalization;
using ClosedXML.Excel;
using ICPFileGenerator.Models;

namespace ICPFileGenerator.Services;

public static class PickupNoticeExcelGenerator
{
    public const string OutputSheetName = "to BE New Pick up notice";

    private static readonly string[] Headers =
    [
        "INVOICE NO.",
        "Ship to address",
        "Customer",
        "RA No.",
        "Ref No.",
        "提貨地點",
        "Contact Person",
        "Phone No.",
        "TET DO#",
        "C/NO",
        "Length",
        "Width",
        "Height",
        "Weight",
        "包裝方式名稱"
    ];

    public static string Generate(
        IReadOnlyList<ShippingAdviceRow> rows,
        string outputDirectory,
        DateTime stampDate)
    {
        Directory.CreateDirectory(outputDirectory);
        var datePart = stampDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(outputDirectory, $"PickupNotice_{datePart}.xlsx");

        var ordered = rows
            .OrderBy(r => r.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => ParseCartonSortKey(r.CartonNo))
            .ThenBy(r => r.CartonNo, StringComparer.OrdinalIgnoreCase)
            .ToList();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(OutputSheetName);

        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = Headers[c];
            sheet.Cell(1, c + 1).Style.Font.Bold = true;
        }

        for (var i = 0; i < ordered.Count; i++)
        {
            var row = ordered[i];
            var excelRow = i + 2;
            sheet.Cell(excelRow, 1).Value = row.InvoiceNo;
            sheet.Cell(excelRow, 2).Value = row.ShipToAddress;
            sheet.Cell(excelRow, 3).Value = row.Customer;
            sheet.Cell(excelRow, 4).Value = row.ColumnK;
            sheet.Cell(excelRow, 5).Value = row.ColumnK;
            sheet.Cell(excelRow, 6).Value = row.ColumnC;
            sheet.Cell(excelRow, 7).Value = row.ColumnC;
            sheet.Cell(excelRow, 8).Value = row.ColumnC;
            sheet.Cell(excelRow, 9).Value = row.TetDo;
            sheet.Cell(excelRow, 10).Value = row.CnoDisplay;
            sheet.Cell(excelRow, 11).Value = row.Length;
            sheet.Cell(excelRow, 12).Value = row.Width;
            sheet.Cell(excelRow, 13).Value = row.Height;
            sheet.Cell(excelRow, 14).Value = row.Weight;
            sheet.Cell(excelRow, 15).Value = row.PackingMethod;
        }

        sheet.Columns().AdjustToContents();
        workbook.SaveAs(filePath);
        return filePath;
    }

    private static int ParseCartonSortKey(string cartonNo)
    {
        return int.TryParse(cartonNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : int.MaxValue;
    }
}
