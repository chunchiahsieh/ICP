namespace TEL.IntegrationHub.Models;

public enum MessageLogStatus
{
    Pending = 0,
    Processing = 1,
    Success = 2,
    Failed = 3,
    Retrying = 4,
    DeadLetter = 5
}
