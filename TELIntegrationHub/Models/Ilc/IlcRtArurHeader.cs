namespace TEL.IntegrationHub.Models.Ilc;

/// <summary>ILC dbo.RT_ARUR_HEADER — only columns written by Hub.</summary>
public sealed class IlcRtArurHeader
{
    public string RtNo { get; set; } = string.Empty;

    public string? CreateBy { get; set; }

    public DateTime CreateDate { get; set; }

    public string? EmailTo { get; set; }

    public string? ShipToCode { get; set; }

    public string? ShipTo { get; set; }

    public DateTime? ArriveDate { get; set; }

    public string? ReceiptInfo { get; set; }

    public string? WhCode { get; set; }

    public string? TetPo { get; set; }

    public string? InvoiceNo { get; set; }

    public string? Attachment { get; set; }

    public string? Mawb { get; set; }

    public string? Hawb { get; set; }

    public string? Flt { get; set; }

    public string? Eta { get; set; }

    public string? Remark { get; set; }

    public string IsSDriver { get; set; } = "N";

    public string IsSStacker { get; set; } = "N";

    public string ArrivalType { get; set; } = "1";

    public string? RequestType { get; set; }

    public string? DependType { get; set; }

    public string Status { get; set; } = "0";

    public string CreateSys { get; set; } = "I";
}
