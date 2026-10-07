namespace ICP.Models.ShipInfo;

public class ShipInfoHeaderQueryModel
{
    public string? DelayNotificationDate { get; set; }
    public string? InvoiceNo { get; set; }

    public Dictionary<string, List<string>> Checkbox { get; set; } = [];

    public Dictionary<string, string> Text { get; set; } = [];

    public Dictionary<string, string> DateFrom { get; set; } = [];

    public Dictionary<string, string> DateTo { get; set; } = [];

    public Dictionary<string, string> Date { get; set; } = [];
}
