using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoHeaderListResult
{
    [JsonPropertyName("items")]
    public IReadOnlyList<Dictionary<string, object?>> Items { get; init; } = [];

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; init; }
}
