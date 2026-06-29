namespace ICP.Models.ShipInfo;

public class ShipInfoTableListViewModel
{
    public string TableId { get; set; } = "datatable";

    public string TableKind { get; set; } = "Header";

    public string Culture { get; set; } = "zh-TW";

    public string? SelectedHeaderKey { get; set; }

    public IReadOnlyList<ShipInfoFieldMetadata> Fields { get; init; } = [];

    public IReadOnlyList<Dictionary<string, object?>> Items { get; init; } = [];
}
