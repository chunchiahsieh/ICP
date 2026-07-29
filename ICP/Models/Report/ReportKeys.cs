namespace ICP.Models.Report;

public static class ReportKeys
{
    public const string ShippingReport = "shippingReport";
    public const string CompareIcpVsArUr = "compareIcpVsArUr";
    public const string MassDataReport = "massDataReport";

    public static bool IsKnown(string? reportKey) =>
        reportKey is not null
        && (reportKey.Equals(ShippingReport, StringComparison.OrdinalIgnoreCase)
            || reportKey.Equals(CompareIcpVsArUr, StringComparison.OrdinalIgnoreCase)
            || reportKey.Equals(MassDataReport, StringComparison.OrdinalIgnoreCase));
}
