namespace TEL.IntegrationHub.Models;

public sealed class IcpAttachment
{
    public Guid Id { get; set; }
    public string AttachmentType { get; set; } = string.Empty;
    public string AttachmentOwnerId { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}
