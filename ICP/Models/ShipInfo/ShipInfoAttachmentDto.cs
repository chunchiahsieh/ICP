namespace ICP.Models.ShipInfo;

public sealed class ShipInfoAttachmentDto
{
    public Guid Id { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string? ContentType { get; init; }
    public DateTime CreateTime { get; init; }
    public string? CreateUser { get; init; }
}
