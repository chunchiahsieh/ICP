using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoSearchCriteria
{
    [JsonPropertyName("filters")]
    public Dictionary<string, string?> Filters { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("page")]
    public int Page { get; set; } = 1;

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; } = 50;
}
