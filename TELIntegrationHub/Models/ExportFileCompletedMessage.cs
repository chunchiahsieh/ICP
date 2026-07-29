using System.Text.Json.Serialization;

namespace TEL.IntegrationHub.Models;

/// <summary>
/// Reserved Export event as standard Envelope (ICP Function/Export publisher not ready yet).
/// </summary>
public class ExportFileCompletedMessage : IntegrationEventEnvelope<ExportFileCompletedPayload>
{
    public ExportFileCompletedMessage()
    {
        EventType = IcpIntegrationEventTypes.ExportCompleted;
        SourceSystem = "ICP";
        Payload = new ExportFileCompletedPayload();
    }
}

public class ExportFileCompletedPayload
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = string.Empty;

    [JsonPropertyName("exportType")]
    public string ExportType { get; init; } = "Function.Export";

    [JsonPropertyName("actor")]
    public ExportFileActor? Actor { get; init; }
}

public class ExportFileActor
{
    [JsonPropertyName("userName")]
    public string UserName { get; init; } = string.Empty;
}
