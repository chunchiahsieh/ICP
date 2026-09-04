using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoDiscardRequest
{
    [JsonPropertyName("headerKey")]
    public string? HeaderKey { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
