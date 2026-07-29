using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoLookupOption
{
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}
