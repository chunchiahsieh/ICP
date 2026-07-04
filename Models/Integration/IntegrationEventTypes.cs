namespace ICP.Models.Integration;

public static class IntegrationEventTypes
{
    public const string ShipInfoCaseInitiated = "icp.shipinfo.case.initiated";
    public const string EventVersion = "1.0";
    public const string Source = "ICP";
}

public static class IntegrationEventOutboxStatuses
{
    public const string Pending = "Pending";
    public const string Published = "Published";
    public const string Failed = "Failed";
}
