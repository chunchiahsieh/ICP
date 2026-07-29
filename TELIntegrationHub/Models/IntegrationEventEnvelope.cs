using System.Text.Json.Serialization;

namespace TEL.IntegrationHub.Models;

/// <summary>Standard integration event envelope (aligned with ICP / Hub design).</summary>
public class IntegrationEventEnvelope<TPayload>
{
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; init; }

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; init; } = "ICP";

    [JsonPropertyName("targetSystems")]
    public IReadOnlyList<string> TargetSystems { get; init; } = [];

    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; init; }

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("payload")]
    public TPayload Payload { get; init; } = default!;
}
