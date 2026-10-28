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
        DateTime stampDate,
        IReadOnlyDictionary<string, PickUpLocationInfo>? pickUpBySloc = null)
    {
        Directory.CreateDirectory(outputDirectory);
        var datePart = stampDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var filePath = Path.Combine(outputDirectory, $"PickupNotice_{datePart}.xlsx");

        var ordered = GroupRowsByCarton(rows)
            .OrderBy(group => group.Row.InvoiceNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => ParseCartonSortKey(group.Row.CartonNo))
            .ThenBy(group => group.Row.CartonNo, StringComparer.OrdinalIgnoreCase)
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
            var row = ordered[i].Row;
            var rmas = CartonRmaFormatter.Join(ordered[i].Rmas);
            var excelRow = i + 2;
            ResolvePickUp(row.ColumnC, pickUpBySloc, out var location, out var contact, out var phone);

            sheet.Cell(excelRow, 1).Value = row.InvoiceNo;
            sheet.Cell(excelRow, 2).Value = row.ShipToAddress;
            sheet.Cell(excelRow, 3).Value = row.Customer;
            sheet.Cell(excelRow, 4).Value = rmas;
            sheet.Cell(excelRow, 5).Value = rmas;
            sheet.Cell(excelRow, 6).Value = location;
            sheet.Cell(excelRow, 7).Value = contact;
            sheet.Cell(excelRow, 8).Value = phone;
            sheet.Cell(excelRow, 9).Value = row.TetDo;
            sheet.Cell(excelRow, 10).Style.NumberFormat.Format = "@";
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

    private static IReadOnlyList<CartonGroup> GroupRowsByCarton(IEnumerable<ShippingAdviceRow> rows)
    {
        var groups = new List<CartonGroup>();
        var firstByCarton = new Dictionary<(string InvoiceNo, string CartonNo), CartonGroup>();

        // Keep the previous invoice/carton matching and first-row selection.
        foreach (var row in rows)
        {
            var cartonNo = row.CartonNo.Trim();
            if (cartonNo.Length == 0)
            {
                groups.Add(new CartonGroup(row));
                continue;
            }

            if (cartonNo.All(c => c is >= '0' and <= '9'))
            {
                cartonNo = cartonNo.TrimStart('0');
                if (cartonNo.Length == 0)
                {
                    cartonNo = "0";
                }
            }

            var key = (row.InvoiceNo.Trim(), cartonNo);
            if (firstByCarton.TryGetValue(key, out var existing))
            {
                existing.Rmas.Add(row.ColumnK);
                continue;
            }

            var group = new CartonGroup(row);
            firstByCarton.Add(key, group);
            groups.Add(group);
        }

        return groups;
    }

    private sealed class CartonGroup(ShippingAdviceRow row)
    {
        public ShippingAdviceRow Row { get; } = row;
        public List<string> Rmas { get; } = [row.ColumnK];
    }

    private static void ResolvePickUp(
        string sloc,
        IReadOnlyDictionary<string, PickUpLocationInfo>? pickUpBySloc,
        out string location,
        out string contact,
        out string phone)
    {
        location = string.Empty;
        contact = string.Empty;
        phone = string.Empty;

        if (pickUpBySloc is null || string.IsNullOrWhiteSpace(sloc))
        {
            return;
        }

        if (pickUpBySloc.TryGetValue(sloc.Trim(), out var info))
        {
            location = info.Location;
            contact = info.ContactPerson;
            phone = info.PhoneNo;
        }
    }

    private static int ParseCartonSortKey(string cartonNo)
    {
        return int.TryParse(cartonNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : int.MaxValue;
    }
}
