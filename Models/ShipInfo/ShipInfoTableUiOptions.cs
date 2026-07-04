using System.Text.Json.Serialization;

namespace ICP.Models.ShipInfo;

public class ShipInfoTableUiOptions
{
    [JsonPropertyName("stickyHeader")]
    public bool? StickyHeader { get; set; }

    [JsonPropertyName("stickyLeftColumns")]
    public bool? StickyLeftColumns { get; set; }

    [JsonPropertyName("maxHeight")]
    public string? MaxHeight { get; set; }

    public static ShipInfoTableUiOptions MergeDefaults(ShipInfoTableUiOptions? source) =>
        new()
        {
            StickyHeader = source?.StickyHeader ?? true,
            StickyLeftColumns = source?.StickyLeftColumns ?? true,
            MaxHeight = string.IsNullOrWhiteSpace(source?.MaxHeight) ? "420px" : source!.MaxHeight
        };
}
