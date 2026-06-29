namespace ICP.Models.ShipInfo;

public class ShipInfoDetailQueryModel
{
    public string? HeaderKey { get; set; }

    public List<string> InvoiceSeq { get; set; } = [];

    public List<string> TetPoLine { get; set; } = [];

    public List<string> ItemNo { get; set; } = [];

    public List<string> Description { get; set; } = [];

    public List<string> Qty { get; set; } = [];

    public List<string> Uom { get; set; } = [];

    public List<string> Coo { get; set; } = [];

    public List<string> Price { get; set; } = [];

    public List<string> Amount { get; set; } = [];

    public List<string> Currency { get; set; } = [];

    public List<string> CartonNo { get; set; } = [];

    public List<string> GrossWeight { get; set; } = [];
}
