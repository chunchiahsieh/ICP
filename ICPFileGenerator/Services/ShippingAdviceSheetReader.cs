using ClosedXML.Excel;
using ICPFileGenerator.Models;

namespace ICPFileGenerator.Services;

public static class ShippingAdviceSheetReader
{
    public const string SourceSheetName = "to BE Shipping advice Report";

    public const int DataStartRow = 4;

    public static IReadOnlyList<ShippingAdviceRow> Read(string inputFilePath)
    {
        if (string.IsNullOrWhiteSpace(inputFilePath) || !File.Exists(inputFilePath))
        {
            throw new FileNotFoundException("Input Excel file was not found.", inputFilePath);
        }

        using var workbook = new XLWorkbook(inputFilePath);
        if (!workbook.Worksheets.TryGetWorksheet(SourceSheetName, out var sheet))
        {
            throw new InvalidOperationException($"Worksheet '{SourceSheetName}' was not found.");
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < DataStartRow)
        {
            return Array.Empty<ShippingAdviceRow>();
        }

        var rows = new List<ShippingAdviceRow>();
        for (var r = DataStartRow; r <= lastRow; r++)
        {
            var invoice = Cell(sheet, r, "N");
            var carton = Cell(sheet, r, "BQ");
            if (string.IsNullOrWhiteSpace(invoice) && string.IsNullOrWhiteSpace(carton))
            {
                continue;
            }

            rows.Add(new ShippingAdviceRow
            {
                InvoiceNo = invoice,
                ShipToAddress = Cell(sheet, r, "BG"),
                Customer = Cell(sheet, r, "Y"),
                ColumnK = Cell(sheet, r, "K"),
                ColumnC = Cell(sheet, r, "C"),
                TetDo = Cell(sheet, r, "P"),
                CartonNo = carton,
                TotalCartons = Cell(sheet, r, "BM"),
                Length = Cell(sheet, r, "BX"),
                Width = Cell(sheet, r, "BY"),
                Height = Cell(sheet, r, "BZ"),
                Weight = Cell(sheet, r, "BW"),
                PackingMethod = Cell(sheet, r, "CA"),
                AhFlag = Cell(sheet, r, "AH"),
                CompanyNameBf = Cell(sheet, r, "BF"),
                PortOfDischargeAu = Cell(sheet, r, "AU"),
                ForwarderBl = Cell(sheet, r, "BL"),
                TeaPoE = Cell(sheet, r, "E"),
                TetSoG = Cell(sheet, r, "G"),
                CustPoJ = Cell(sheet, r, "J")
            });
        }

        return rows;
    }

    private static string Cell(IXLWorksheet sheet, int row, string columnLetter)
    {
        var value = sheet.Cell(row, columnLetter).GetFormattedString()?.Trim();
        return value ?? string.Empty;
    }
}
