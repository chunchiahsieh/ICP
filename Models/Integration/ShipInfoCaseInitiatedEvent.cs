using System.Text.Json.Serialization;
using ICP.Models.ShipInfo;

namespace ICP.Models.Integration;

public class ShipInfoCaseInitiatedEvent
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; init; }

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = IntegrationEventTypes.ShipInfoCaseInitiated;

    [JsonPropertyName("eventVersion")]
    public string EventVersion { get; init; } = IntegrationEventTypes.EventVersion;

    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; init; }

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = string.Empty;

    [JsonPropertyName("source")]
    public string Source { get; init; } = IntegrationEventTypes.Source;

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
