using System.Text.Json.Serialization;

namespace ICP.Models.Integration;

/// <summary>Standard integration event envelope (TEL Integration Hub contract).</summary>
public class IntegrationEventEnvelope<TPayload>
{
    [JsonPropertyName("messageId")]
    public Guid MessageId { get; init; }

    [JsonPropertyName("eventType")]
    public string EventType { get; init; } = string.Empty;

    [JsonPropertyName("sourceSystem")]
    public string SourceSystem { get; init; } = IntegrationEventTypes.Source;

    [JsonPropertyName("targetSystems")]
    public IReadOnlyList<string> TargetSystems { get; init; } = [];

    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; init; }

    [JsonPropertyName("correlationId")]
    public string CorrelationId { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = IntegrationEventTypes.EventVersion;

    [JsonPropertyName("payload")]
    public TPayload Payload { get; init; } = default!;
}
