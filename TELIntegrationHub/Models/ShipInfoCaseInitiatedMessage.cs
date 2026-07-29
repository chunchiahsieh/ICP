using System.Text.Json.Serialization;

namespace TEL.IntegrationHub.Models;

/// <summary>ICP Ship Info case-initiated event (standard Envelope).</summary>
public class ShipInfoCaseInitiatedMessage : IntegrationEventEnvelope<ShipInfoCaseInitiatedPayload>
{
    public ShipInfoCaseInitiatedMessage()
    {
        EventType = IcpIntegrationEventTypes.ShipInfoCaseInitiated;
        SourceSystem = "ICP";
        Payload = new ShipInfoCaseInitiatedPayload();
    }
}

public class ShipInfoCaseInitiatedPayload
{
    [JsonPropertyName("caseType")]
    public string CaseType { get; init; } = string.Empty;

    [JsonPropertyName("caseNo")]
    public string CaseNo { get; init; } = string.Empty;

    [JsonPropertyName("headerKey")]
    public string HeaderKey { get; init; } = string.Empty;

    [JsonPropertyName("oldStatus")]
    public string? OldStatus { get; init; }

    [JsonPropertyName("newStatus")]
    public string? NewStatus { get; init; }

    [JsonPropertyName("actor")]
    public ShipInfoCaseActor? Actor { get; init; }

    [JsonPropertyName("snapshot")]
    public object? Snapshot { get; init; }
}

public class ShipInfoCaseActor
{
    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;
}
