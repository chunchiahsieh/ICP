using System.Text.Json;
using System.Text.Json.Nodes;

namespace TEL.IntegrationHub.Services;

/// <summary>
/// Normalizes legacy flat ICP payloads (eventId/source) into standard Envelope.
/// Remove after Outbox Pending backlog is drained.
/// </summary>
public static class IntegrationEventEnvelopeNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static bool TryNormalizeShipInfoCase(
        string rawJson,
        out Models.ShipInfoCaseInitiatedMessage? envelope,
        out string normalizedJson)
    {
        envelope = null;
        normalizedJson = rawJson;

        try
        {
            var node = JsonNode.Parse(rawJson) as JsonObject;
            if (node is null)
            {
                return false;
            }

            // Already Envelope
            if (node.ContainsKey("messageId") && node.ContainsKey("payload"))
            {
                envelope = JsonSerializer.Deserialize<Models.ShipInfoCaseInitiatedMessage>(rawJson, JsonOptions);
                if (envelope?.Payload is null)
                {
                    return false;
                }

                normalizedJson = rawJson;
                return true;
            }

            // Legacy flat schema
            if (!node.ContainsKey("eventId") && !node.ContainsKey("EventId"))
            {
                return false;
            }

            var legacy = JsonSerializer.Deserialize<LegacyShipInfoCase>(rawJson, JsonOptions);
            if (legacy is null)
            {
                return false;
            }

            envelope = new Models.ShipInfoCaseInitiatedMessage
            {
                MessageId = legacy.EventId != Guid.Empty ? legacy.EventId : Guid.NewGuid(),
                EventType = string.IsNullOrWhiteSpace(legacy.EventType)
                    ? Models.IcpIntegrationEventTypes.ShipInfoCaseInitiated
                    : legacy.EventType,
                SourceSystem = string.IsNullOrWhiteSpace(legacy.Source) ? "ICP" : legacy.Source,
                TargetSystems = ["GEM", "ARUR"],
                OccurredAt = legacy.OccurredAt,
                CorrelationId = legacy.CorrelationId ?? string.Empty,
                Version = string.IsNullOrWhiteSpace(legacy.EventVersion) ? "1.0" : legacy.EventVersion,
                Payload = new Models.ShipInfoCaseInitiatedPayload
                {
                    CaseType = legacy.CaseType ?? string.Empty,
                    CaseNo = legacy.CaseNo ?? string.Empty,
                    HeaderKey = legacy.HeaderKey ?? string.Empty,
                    OldStatus = legacy.OldStatus,
                    NewStatus = legacy.NewStatus,
                    Actor = legacy.Actor,
                    Snapshot = legacy.Snapshot
                }
            };

            normalizedJson = JsonSerializer.Serialize(envelope, JsonOptions);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private sealed class LegacyShipInfoCase
    {
        public Guid EventId { get; init; }
        public string? EventType { get; init; }
        public string? EventVersion { get; init; }
        public DateTime OccurredAt { get; init; }
        public string? CorrelationId { get; init; }
        public string? Source { get; init; }
        public string? CaseType { get; init; }
        public string? CaseNo { get; init; }
        public string? HeaderKey { get; init; }
        public string? OldStatus { get; init; }
        public string? NewStatus { get; init; }
        public Models.ShipInfoCaseActor? Actor { get; init; }
        public object? Snapshot { get; init; }
    }
}
