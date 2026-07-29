using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoSaveRequest
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("headerKey")]
    public string? HeaderKey { get; set; }

    [JsonPropertyName("values")]
    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("rowVersion")]
    public string? RowVersion { get; set; }

    [JsonPropertyName("updateTime")]
    public string? UpdateTime { get; set; }
}
