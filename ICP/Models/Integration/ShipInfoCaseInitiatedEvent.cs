using System.Text.Json.Serialization;
using ICP.Models.ShipInfo;

namespace ICP.Models.Integration;

/// <summary>
/// Ship Info case-initiated event as standard Envelope.
/// Business fields live under <see cref="IntegrationEventEnvelope{TPayload}.Payload"/>.
/// </summary>
public class ShipInfoCaseInitiatedEvent : IntegrationEventEnvelope<ShipInfoCaseInitiatedPayload>
{
    public ShipInfoCaseInitiatedEvent()
    {
        EventType = IntegrationEventTypes.ShipInfoCaseInitiated;
        SourceSystem = IntegrationEventTypes.Source;
        Version = IntegrationEventTypes.EventVersion;
        TargetSystems = IntegrationEventTypes.ShipInfoCaseDefaultTargets;
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
    public ShipInfoCaseEventActor Actor { get; init; } = new();

    [JsonPropertyName("snapshot")]
    public ShipInfoCaseEventSnapshot Snapshot { get; init; } = new();
}

public class ShipInfoCaseEventActor
{
    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;
}

public class ShipInfoCaseEventSnapshot
{
    [JsonPropertyName("header")]
    public Dictionary<string, object?> Header { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("details")]
    public IReadOnlyList<Dictionary<string, object?>> Details { get; init; } = [];

    [JsonPropertyName("headerSummary")]
    public ShipInfoHeaderSummaryDto HeaderSummary { get; init; } = new();

    [JsonPropertyName("detailSummary")]
    public ShipInfoDetailSummaryDto DetailSummary { get; init; } = new();
}
