namespace TEL.IntegrationHub.Services;

public sealed class ExportRequestNotFoundException : Exception
{
    public Guid RequestId { get; }

    public string? DataSource { get; }

    public string? InitialCatalog { get; }

    public ExportRequestNotFoundException(
        Guid requestId,
        string? dataSource,
        string? initialCatalog)
        : base(BuildMessage(requestId, dataSource, initialCatalog))
    {
        RequestId = requestId;
        DataSource = dataSource;
        InitialCatalog = initialCatalog;
    }

    private static string BuildMessage(Guid requestId, string? dataSource, string? initialCatalog) =>
        $"EXPORT_REQUEST {requestId:D} not found on Hub ICP_Connection " +
        $"(Data Source={dataSource ?? "?"}; Initial Catalog={initialCatalog ?? "?"}). " +
        "Confirm ICP and Hub ConnectionStrings:ICP_Connection point to the same database.";
}
