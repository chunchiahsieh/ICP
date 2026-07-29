namespace ICP.Models;

public class TariffDataOptions
{
    public const string SectionName = "TariffData";

    public string StoragePath { get; set; } = "uploads/broker/tariff";

    public int MaxSizeMb { get; set; } = 50;

    public TariffDataBrokerKeywordsOptions BrokerKeywords { get; set; } = new();

    public long MaxSizeBytes => MaxSizeMb * 1024L * 1024L;
}
