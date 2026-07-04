using ICP.Models.Icp;

namespace ICP.Helpers;

public static class ForwarderImportRules
{
    public static void ValidatePerInvoiceFieldConsistency(
        IReadOnlyList<ForwarderDataUpload> rows,
        List<string> errors)
    {
        var groups = rows
            .GroupBy(row => row.InvoiceNo.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            if (group.Count() <= 1)
            {
                continue;
            }

            var reference = group.First();
            var hasMismatch = group.Skip(1).Any(row =>
                !EqualsNormalized(reference.Mawb, row.Mawb)
                || !EqualsNormalized(reference.Hawb, row.Hawb)
                || !EqualsNormalized(reference.Flight1, row.Flight1)
                || !EqualsNormalized(reference.Flight2, row.Flight2)
                || !EqualsDate(reference.Eta, row.Eta));

            if (hasMismatch)
            {
                errors.Add(
                    $"Invoice {group.Key} 多筆明細的 MAWB/HAWB/FLT1#/FLT2#/ETA 須一致，請修正後重新上傳");
            }
        }
    }

    public static IReadOnlyList<string> FindDuplicateInvoiceNumbers(
        IEnumerable<string> invoiceNumbers,
        IEnumerable<string> existingInvoiceNumbers)
    {
        var incoming = invoiceNumbers
            .Where(invoice => !string.IsNullOrWhiteSpace(invoice))
            .Select(invoice => invoice.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (incoming.Count == 0)
        {
            return [];
        }

        return existingInvoiceNumbers
            .Where(invoice => !string.IsNullOrWhiteSpace(invoice))
            .Select(invoice => invoice.Trim())
            .Where(incoming.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(invoice => invoice, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool EqualsNormalized(string? left, string? right) =>
        string.Equals(NormalizeText(left), NormalizeText(right), StringComparison.OrdinalIgnoreCase);

    private static bool EqualsDate(DateTime? left, DateTime? right) =>
        NormalizeDate(left) == NormalizeDate(right);

    private static string NormalizeText(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    private static string NormalizeDate(DateTime? value) =>
        value?.Date.ToString("yyyy-MM-dd") ?? string.Empty;
}
