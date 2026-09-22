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

        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in sheet.Row(1).CellsUsed())
        {
            var title = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(title))
            {
                columns.TryAdd(title, cell.Address.ColumnNumber);
            }
        }

        // Resolve by title so the inserted DO line Remark column cannot shift exported values.
        string Value(int row, string title)
        {
            if (!columns.TryGetValue(title, out var column))
            {
                throw new InvalidOperationException($"Required Shipping advice column '{title}' was not found.");
            }

            return sheet.Cell(row, column).GetFormattedString().Trim();
        }

        var required = new[]
        {
            "Invoice No.", "Ship-to Party Address", "End User", "RMA#", "SLOC",
            "Delivery No", "Carton No.", "Total Cartons", "Length", "Width",
            "Height", "Weight", "Carton Name", "No Charge Flag",
            "Ship-to Party", "Port of Entry", "Forwarder", "PO#", "SO#",
            "Customer PO No.", "Country of Origin"
        };
        foreach (var title in required)
        {
            if (!columns.ContainsKey(title))
            {
                throw new InvalidOperationException($"Required Shipping advice column '{title}' was not found.");
            }
        }

        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
        if (lastRow < DataStartRow)
        {
            return Array.Empty<ShippingAdviceRow>();
        }

        var rows = new List<ShippingAdviceRow>();
        for (var r = DataStartRow; r <= lastRow; r++)
        {
            var invoice = Value(r, "Invoice No.");
            var carton = Value(r, "Carton No.");
            if (string.IsNullOrWhiteSpace(invoice) && string.IsNullOrWhiteSpace(carton))
            {
                continue;
            }

            rows.Add(new ShippingAdviceRow
            {
                InvoiceNo = invoice,
                ShipToAddress = Value(r, "Ship-to Party Address"),
                Customer = Value(r, "End User"),
                ColumnK = Value(r, "RMA#"),
                ColumnC = Value(r, "SLOC"),
                TetDo = Value(r, "Delivery No"),
                CartonNo = carton,
                TotalCartons = Value(r, "Total Cartons"),
                Length = Value(r, "Length"),
                Width = Value(r, "Width"),
                Height = Value(r, "Height"),
                Weight = Value(r, "Weight"),
                PackingMethod = Value(r, "Carton Name"),
                AhFlag = Value(r, "No Charge Flag"),
                CompanyNameBf = Value(r, "Ship-to Party"),
                PortOfDischargeAu = Value(r, "Port of Entry"),
                ForwarderBl = Value(r, "Forwarder"),
                TeaPoE = Value(r, "PO#"),
                TetSoG = Value(r, "SO#"),
                CustPoJ = Value(r, "Customer PO No."),
                CountryOfOriginCode = Value(r, "Country of Origin")
            });
        }

        return rows;
    }

}
