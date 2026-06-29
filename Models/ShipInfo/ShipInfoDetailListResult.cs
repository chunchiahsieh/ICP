using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoDetailListResult
{
    [JsonPropertyName("headerKey")]
    public string HeaderKey { get; init; } = string.Empty;

    [JsonPropertyName("items")]
    public IReadOnlyList<Dictionary<string, object?>> Items { get; init; } = [];
}
