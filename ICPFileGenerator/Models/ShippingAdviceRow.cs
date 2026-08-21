namespace ICPFileGenerator.Models;

public sealed class ShippingAdviceRow
{
    public string InvoiceNo { get; init; } = string.Empty;

    public string ShipToAddress { get; init; } = string.Empty;

    public string Customer { get; init; } = string.Empty;

    public string ColumnK { get; init; } = string.Empty;

    public string ColumnC { get; init; } = string.Empty;

    public string TetDo { get; init; } = string.Empty;

    public string CartonNo { get; init; } = string.Empty;

    public string TotalCartons { get; init; } = string.Empty;

    public string Length { get; init; } = string.Empty;

    public string Width { get; init; } = string.Empty;

    public string Height { get; init; } = string.Empty;

    public string Weight { get; init; } = string.Empty;

    public string PackingMethod { get; init; } = string.Empty;

    public string AhFlag { get; init; } = string.Empty;

    public string CompanyNameBf { get; init; } = string.Empty;

    public string PortOfDischargeAu { get; init; } = string.Empty;

    public string ForwarderBl { get; init; } = string.Empty;

    public string TeaPoE { get; init; } = string.Empty;

    public string TetSoG { get; init; } = string.Empty;

    public string CustPoJ { get; init; } = string.Empty;

    public bool IsNoCharge =>
        string.Equals(AhFlag.Trim(), "X", StringComparison.OrdinalIgnoreCase);

    public string CnoDisplay =>
        string.IsNullOrWhiteSpace(CartonNo) && string.IsNullOrWhiteSpace(TotalCartons)
            ? string.Empty
            : $"{CartonNo}/{TotalCartons}";

    public string SizeDisplay
    {
        get
        {
            var l = Length.Trim();
            var w = Width.Trim();
            var h = Height.Trim();
            if (string.IsNullOrWhiteSpace(l) && string.IsNullOrWhiteSpace(w) && string.IsNullOrWhiteSpace(h))
            {
                return string.Empty;
            }

            return $"{l} * {w} * {h} CM";
        }
    }
}
