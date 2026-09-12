namespace ICP.Models.LocalizationManagement;

public sealed class LocalizationResourceRow
{
    public string Key { get; init; } = string.Empty;

    public string Default { get; init; } = string.Empty;

    public string ZhTw { get; init; } = string.Empty;

    public string En { get; init; } = string.Empty;

    public string Ja { get; init; } = string.Empty;
}

public sealed class LocalizationResourceSaveRequest
{
    public List<LocalizationResourceRow> Rows { get; init; } = [];
}
