namespace ICP.Models;

public class TariffDataImportResult
{
    public int ImportedCount { get; init; }

    public int UpdatedCount { get; init; }

    public int TotalCount => ImportedCount + UpdatedCount;
}
