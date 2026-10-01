using System.Globalization;
using System.Text;
using ICPFileGenerator.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ICPFileGenerator.Services;

public static class CaseMarkPdfGenerator
{
    static CaseMarkPdfGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static IReadOnlyList<string> GenerateAll(
        IReadOnlyList<ShippingAdviceRow> rows,
        string outputDirectory,
        DateTime stampDate)
    {
        Directory.CreateDirectory(outputDirectory);
        var datePart = stampDate.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var created = new List<string>();

        var groups = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.InvoiceNo))
            .GroupBy(r => r.InvoiceNo.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var orderedRows = group
                .OrderBy(r => ParseCartonSortKey(r.CartonNo))
                .ThenBy(r => r.CartonNo, StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (orderedRows.Count == 0)
            {
                continue;
            }

            // One case mark per carton; multiple item rows in the same carton share its origin list.
            var pages = orderedRows
                .Select((row, index) => new
                {
                    Row = row,
                    // Keep the original grouping key independent of C/NO display formatting.
                    Key = string.IsNullOrWhiteSpace(row.CnoDisplay)
                        ? $"__empty_carton_{index}"
                        : $"{row.CartonNo}/{row.TotalCartons}"
                })
                .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(carton => new
                {
                    Row = carton.First().Row,
                    Origins = string.Join(" & ", carton
                        .Select(item => item.Row.CountryOfOriginName)
                        .Distinct(StringComparer.OrdinalIgnoreCase))
                })
                .ToList();

            var first = pages[0].Row;
            var isNoCharge = first.IsNoCharge;
            var prefix = isNoCharge ? "NoCharge" : "Charge";
            var safeInvoice = SanitizeFileName(group.Key);
            var fileName = $"{prefix}_{safeInvoice}_{datePart}.pdf";
            var filePath = Path.Combine(outputDirectory, fileName);

            Document.Create(container =>
            {
                foreach (var carton in pages)
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(40);
                        page.DefaultTextStyle(x => x.FontSize(14).FontFamily(Fonts.Arial));
                        page.Content().Column(col =>
                        {
                            col.Item().Border(1).Padding(10).Column(frame =>
                            {
                                frame.Spacing(8);
                                frame.Item().AlignCenter().Text("**** CASE MARK ****").Bold().FontSize(18);

                                if (isNoCharge)
                                {
                                    BuildNoCharge(frame, carton.Row);
                                }
                                else
                                {
                                    BuildCharge(frame, carton.Row);
                                }
                            });
                            col.Item().AlignRight().Text($"Made in {carton.Origins}").Bold().FontSize(14);
                        });
                    });
                }
            }).GeneratePdf(filePath);

            created.Add(filePath);
        }

        return created;
    }

    private static void BuildNoCharge(ColumnDescriptor col, ShippingAdviceRow row)
    {
        col.Item().Text(row.CompanyNameBf);
        col.Item().Text($"PORT OF DISCHARGE : {row.PortOfDischargeAu}");
        col.Item().Text($"REF : {row.ColumnK}");
        col.Item().Text($"INV NO. : {row.InvoiceNo}").Bold().FontSize(22);
        col.Item().Text($"FORWARDER : {row.ForwarderBl}");
        col.Item().Text($"C/NO. : {row.CnoDisplay}");
        col.Item().Text($"SIZE(L*W*H) : {row.SizeDisplay}");
    }

    private static void BuildCharge(ColumnDescriptor col, ShippingAdviceRow row)
    {
        col.Item().Text(row.SoldToCompany);
        col.Item().Text($"PORT OF DISCHARGE : {row.PortOfDischargeAu}");
        col.Item().Text($"PO NO. : {row.TeaPoE}");
        col.Item().Text($"TET SO NO. : {row.TetSoG}");
        col.Item().Text($"CUST PO NO. : {row.CustPoJ}");
        col.Item().Text($"INV NO. : {row.InvoiceNo}").Bold().FontSize(18);
        col.Item().Text($"FORWARDER : {row.ForwarderBl}");
        col.Item().Text($"C/NO. : {row.CnoDisplay}");
        col.Item().Text($"SIZE(L*W*H) : {row.SizeDisplay}");
    }

    private static int ParseCartonSortKey(string cartonNo)
    {
        return int.TryParse(cartonNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : int.MaxValue;
    }

    private static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "UNKNOWN";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return sb.ToString();
    }
}
