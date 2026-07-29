namespace TEL.IntegrationHub.Models;

public static class IcpIntegrationBusinessTypes
{
    public const string Deposit = "Deposit";
    public const string Arur = "ARUR";
    public const string Export = "Export";
}

public static class IcpIntegrationEventTypes
{
    public const string ShipInfoCaseInitiated = "icp.shipinfo.case.initiated";
    public const string ExportCompleted = "icp.export.completed";
}
