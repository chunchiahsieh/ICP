namespace ICP.Models;

public class TariffDataSearchModel
{
    public List<string> MAWBs { get; set; } = [];

    public List<string> HAWBs { get; set; } = [];

    public List<string> ImportDates { get; set; } = [];

    public List<string> DeclarationDates { get; set; } = [];

    public List<string> ReleaseDates { get; set; } = [];

    public List<string> InvoiceNumbers { get; set; } = [];

    public List<string> DescriptionOfGoodsList { get; set; } = [];

    public List<string> HTSNumbers { get; set; } = [];

    public List<string> EntryNumbers { get; set; } = [];

    public List<string> Modes { get; set; } = [];

    public List<string> PortOfDepartures { get; set; } = [];

    public List<string> FlightNos { get; set; } = [];

    public List<string> Shippers { get; set; } = [];

    public List<string> Brokers { get; set; } = [];

    public List<string> AirSeas { get; set; } = [];

    public List<string> CreateDates { get; set; } = [];
}
